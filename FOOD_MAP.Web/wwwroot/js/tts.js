window.foodMapTts = {

    _queue: [],       // Hàng đợi các câu chờ đọc
    _isSpeaking: false,
    _heartbeat: null, // Chrome keep-alive timer
    _audioElement: null,
    _speechStarted: false,
    _speechError: '',
    _lastAudioError: '',
    _audioObjectUrl: '',

    _buildAbsoluteUrl: function (rawUrl) {
        var normalized = (rawUrl || '').trim();
        if (!normalized) {
            return '';
        }

        try {
            var parsed = new URL(normalized, window.location.origin);

            // Khi data lưu localhost/127.0.0.1 thì điện thoại sẽ không truy cập được.
            // Rewrite về chính host hiện tại (dev tunnel/domain đang mở trên mobile).
            if (parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1') {
                parsed.protocol = window.location.protocol;
                parsed.host = window.location.host;
            }

            return parsed.toString();
        } catch (e) {
            console.warn('[TTS] URL audio không hợp lệ:', normalized, e);
            return '';
        }
    },

    _buildAudioUrlCandidates: function (rawUrl) {
        var normalized = (rawUrl || '').trim();
        if (!normalized) {
            return [];
        }

        var candidates = [];
        var absoluteUrl = this._buildAbsoluteUrl(normalized);
        if (!absoluteUrl) {
            return [];
        }

        candidates.push(absoluteUrl);

        var isLocalAudioPath = normalized.indexOf('audio/') === 0 || normalized.indexOf('/audio/') === 0;
        if (isLocalAudioPath) {
            var sharedPath = normalized.startsWith('/')
                ? '/_content/FOOD_MAP.Shared' + normalized
                : '/_content/FOOD_MAP.Shared/' + normalized;
            var sharedAbsoluteUrl = this._buildAbsoluteUrl(sharedPath);
            if (sharedAbsoluteUrl && candidates.indexOf(sharedAbsoluteUrl) < 0) {
                candidates.push(sharedAbsoluteUrl);
            }

            // Fallback cho dữ liệu đang lưu theo audio/{lang}/file.mp3 nhưng file thật nằm ở audio/file.mp3.
            var localParts = normalized.replace(/^\/?audio\//i, '').split('/');
            if (localParts.length >= 2) {
                var strippedPath = 'audio/' + localParts[localParts.length - 1];
                var strippedAbsoluteUrl = this._buildAbsoluteUrl(strippedPath);
                var strippedSharedPath = '/_content/FOOD_MAP.Shared/' + strippedPath;
                var strippedSharedAbsoluteUrl = this._buildAbsoluteUrl(strippedSharedPath);

                if (strippedAbsoluteUrl && candidates.indexOf(strippedAbsoluteUrl) < 0) {
                    candidates.push(strippedAbsoluteUrl);
                }

                if (strippedSharedAbsoluteUrl && candidates.indexOf(strippedSharedAbsoluteUrl) < 0) {
                    candidates.push(strippedSharedAbsoluteUrl);
                }
            }
        }

        return candidates;
    },

    _readAudioElementError: function () {
        if (!this._audioElement || !this._audioElement.error) {
            return '';
        }

        var mediaError = this._audioElement.error;
        var code = mediaError.code;
        if (code === 1) return 'MEDIA_ERR_ABORTED';
        if (code === 2) return 'MEDIA_ERR_NETWORK';
        if (code === 3) return 'MEDIA_ERR_DECODE';
        if (code === 4) return 'MEDIA_ERR_SRC_NOT_SUPPORTED';
        return 'MEDIA_ERR_UNKNOWN';
    },

    _guessMimeTypeFromUrl: function (audioUrl) {
        var lower = (audioUrl || '').toLowerCase();
        if (lower.indexOf('.mp3') >= 0) return 'audio/mpeg';
        if (lower.indexOf('.m4a') >= 0 || lower.indexOf('.mp4') >= 0) return 'audio/mp4';
        if (lower.indexOf('.aac') >= 0) return 'audio/aac';
        if (lower.indexOf('.wav') >= 0) return 'audio/wav';
        if (lower.indexOf('.ogg') >= 0) return 'audio/ogg';
        if (lower.indexOf('.webm') >= 0) return 'audio/webm';
        return 'audio/mpeg';
    },

    _isSrcNotSupportedCase: function (browserError, mediaError) {
        if ((browserError || '').toLowerCase() === 'notsupportederror') {
            return true;
        }

        return (mediaError || '').indexOf('MEDIA_ERR_SRC_NOT_SUPPORTED') >= 0;
    },

    _buildEmergencyTtsUrl: function (text, languageCode) {
        var plain = (text || '').trim();
        if (!plain) {
            return '';
        }

        // Endpoint public, không cần API key; giới hạn query ngắn để tránh URL quá dài.
        var compact = plain.replace(/\s+/g, ' ').trim();
        var capped = compact.length > 180 ? compact.substring(0, 180) : compact;
        var lang = (languageCode || 'vi').split('-')[0].toLowerCase();
        return 'https://translate.googleapis.com/translate_tts?ie=UTF-8&client=tw-ob&tl=' +
            encodeURIComponent(lang) + '&q=' + encodeURIComponent(capped);
    },

    _playAudioViaBlob: async function (audioUrl) {
        var response = await fetch(audioUrl, {
            method: 'GET',
            mode: 'cors',
            cache: 'no-store'
        });

        if (!response.ok) {
            throw new Error('audio-fetch-http-' + response.status);
        }

        var buffer = await response.arrayBuffer();
        if (!buffer || buffer.byteLength === 0) {
            throw new Error('audio-fetch-empty');
        }

        var headerType = (response.headers.get('content-type') || '').split(';')[0].trim();
        var blobType = headerType || this._guessMimeTypeFromUrl(audioUrl);
        var blob = new Blob([buffer], { type: blobType });

        if (this._audioObjectUrl) {
            URL.revokeObjectURL(this._audioObjectUrl);
            this._audioObjectUrl = '';
        }

        this._audioObjectUrl = URL.createObjectURL(blob);
        this._audioElement.pause();
        this._audioElement.src = this._audioObjectUrl;
        this._audioElement.currentTime = 0;
        this._audioElement.load();
        await this._audioElement.play();
        console.log('[TTS] Đang phát audio fallback bằng Blob URL.');
    },

    _playAudioFallback: async function (rawUrl) {
        this._lastAudioError = '';
        var audioUrlCandidates = this._buildAudioUrlCandidates(rawUrl);
        if (audioUrlCandidates.length === 0) {
            this._lastAudioError = 'audio-url-empty';
            return false;
        }

        var lastError = '';
        for (var i = 0; i < audioUrlCandidates.length; i++) {
            var audioUrl = audioUrlCandidates[i];

            try {
                this.stop();

                if (!this._audioElement) {
                    this._audioElement = new Audio();
                    this._audioElement.preload = 'auto';
                    this._audioElement.playsInline = true;
                }

                this._audioElement.pause();
                this._audioElement.src = audioUrl;
                this._audioElement.currentTime = 0;
                this._audioElement.load();
                await this._audioElement.play();
                console.log('[TTS] Đang phát audio fallback:', audioUrl);
                return true;
            } catch (e) {
                var browserError = e && e.name ? e.name : 'audio-play-failed';
                var mediaError = this._readAudioElementError();
                lastError = mediaError ? (browserError + '|' + mediaError) : browserError;
                this._lastAudioError = lastError;
                console.error('[TTS] Không phát được audio fallback trực tiếp:', audioUrl, e);

                if (!this._isSrcNotSupportedCase(browserError, mediaError)) {
                    continue;
                }

                try {
                    await this._playAudioViaBlob(audioUrl);
                    this._lastAudioError = '';
                    return true;
                } catch (blobErr) {
                    var blobReason = blobErr && blobErr.message ? blobErr.message : 'blob-play-failed';
                    this._lastAudioError = this._lastAudioError + '|blob=' + blobReason;
                    console.error('[TTS] Blob fallback vẫn thất bại:', blobErr);
                }
            }

            // Thử candidate tiếp theo, ví dụ /audio/... rồi _content/FOOD_MAP.Shared/audio/...
        }

        this._lastAudioError = lastError || this._lastAudioError || 'audio-play-failed';
        return false;
    },

    _playEmergencyTtsAudio: async function (text, languageCode) {
        var emergencyUrl = this._buildEmergencyTtsUrl(text, languageCode);
        if (!emergencyUrl) {
            return false;
        }

        var played = await this._playAudioFallback(emergencyUrl);
        if (played) {
            console.warn('[TTS] Đang dùng emergency cloud TTS fallback.');
        }
        return played;
    },

    // Chờ voices load xong (Chrome load bất đồng bộ lần đầu).
    _loadVoices: function () {
        return new Promise(function (resolve) {
            var voices = window.speechSynthesis.getVoices();
            if (voices.length > 0) { resolve(voices); return; }

            var resolved = false;
            function handler() {
                if (resolved) return;
                resolved = true;
                window.speechSynthesis.removeEventListener('voiceschanged', handler);
                resolve(window.speechSynthesis.getVoices());
            }
            window.speechSynthesis.addEventListener('voiceschanged', handler);
            setTimeout(function () {
                if (!resolved) { resolved = true; resolve(window.speechSynthesis.getVoices()); }
            }, 2000);
        });
    },

    // Chia text thành câu ngắn để tránh Chrome bug dừng giữa chừng.
    _splitSentences: function (text) {
        // Tách theo dấu câu; giữ dấu ở cuối câu.
        var raw = text.split(/(?<=[.!?…\n])\s+|(?<=\n)/);
        var sentences = [];
        var chunk = '';
        for (var i = 0; i < raw.length; i++) {
            var seg = raw[i].trim();
            if (!seg) continue;
            // Giữ chunk tối đa 150 ký tự để Chrome không bị timeout.
            if ((chunk + ' ' + seg).trim().length > 150 && chunk.length > 0) {
                sentences.push(chunk.trim());
                chunk = seg;
            } else {
                chunk = chunk ? chunk + ' ' + seg : seg;
            }
        }
        if (chunk.trim()) sentences.push(chunk.trim());
        return sentences.length > 0 ? sentences : [text.trim()];
    },

    // Bật heartbeat để Chrome không ngừng speechSynthesis khi tab mất focus.
    _startHeartbeat: function () {
        var self = this;
        if (self._heartbeat) return;
        self._heartbeat = setInterval(function () {
            if (!window.speechSynthesis.speaking) {
                clearInterval(self._heartbeat);
                self._heartbeat = null;
                return;
            }
            // Pause/resume trick ngăn Chrome freeze sau ~15 giây.
            window.speechSynthesis.pause();
            window.speechSynthesis.resume();
        }, 10000);
    },

    _stopHeartbeat: function () {
        if (this._heartbeat) {
            clearInterval(this._heartbeat);
            this._heartbeat = null;
        }
    },

    // Phát câu tiếp theo trong hàng đợi.
    _speakNext: function (voice, lang) {
        var self = this;
        if (self._queue.length === 0) {
            self._isSpeaking = false;
            self._stopHeartbeat();
            console.log('[TTS] Đã đọc xong tất cả câu.');
            return;
        }

        var sentence = self._queue.shift();
        var utt = new SpeechSynthesisUtterance(sentence);
        utt.lang = lang;
        utt.rate = 0.95;   // Hơi chậm hơn mặc định, tiếng Việt nghe rõ hơn.
        utt.pitch = 1.0;
        utt.volume = 1.0;
        if (voice) utt.voice = voice;

        utt.onstart = function () {
            self._speechStarted = true;
        };

        utt.onend = function () {
            // Delay nhỏ giữa các câu để Chrome không bị lỗi.
            setTimeout(function () { self._speakNext(voice, lang); }, 100);
        };
        utt.onerror = function (e) {
            console.error('[TTS] Lỗi câu:', e.error, '|', sentence.substring(0, 40));
            if (!self._speechStarted) {
                self._speechError = e && e.error ? e.error : 'speech-error';
            }
            // Bỏ qua câu lỗi, đọc câu tiếp.
            setTimeout(function () { self._speakNext(voice, lang); }, 200);
        };
        try {
            window.speechSynthesis.resume();
        } catch (e) {
            console.warn('[TTS] resume() thất bại:', e);
        }
        window.speechSynthesis.speak(utt);
    },

    speak: async function (text, languageCode, fallbackAudioUrl) {
        var self = this;
        var normalizedText = (text || '').trim();
        var hasFallbackAudio = !!self._buildAbsoluteUrl(fallbackAudioUrl);

        if (!window.speechSynthesis) {
            if (hasFallbackAudio) {
                var playedByAudio = await self._playAudioFallback(fallbackAudioUrl);
                return {
                    ok: playedByAudio,
                    mode: playedByAudio ? 'audio' : 'none',
                    message: playedByAudio
                        ? 'speechSynthesis không hỗ trợ, đã fallback sang audio.'
                        : 'speechSynthesis không hỗ trợ và không phát được audio fallback.'
                };
            }

            var playedEmergencyWithoutSpeech = await self._playEmergencyTtsAudio(normalizedText, languageCode);
            if (playedEmergencyWithoutSpeech) {
                return {
                    ok: true,
                    mode: 'audio',
                    message: 'speechSynthesis không hỗ trợ, đã dùng emergency cloud TTS.'
                };
            }

            console.warn('[TTS] speechSynthesis không được hỗ trợ.');
            return {
                ok: false,
                mode: 'none',
                message: 'speechSynthesis không được hỗ trợ.'
            };
        }

        if (!normalizedText) {
            if (hasFallbackAudio) {
                var playedEmptyByAudio = await self._playAudioFallback(fallbackAudioUrl);
                return {
                    ok: playedEmptyByAudio,
                    mode: playedEmptyByAudio ? 'audio' : 'none',
                    message: playedEmptyByAudio
                        ? 'Không có text TTS, đã phát audio fallback.'
                        : 'Không có text TTS và audio fallback không phát được.'
                };
            }

            console.warn('[TTS] Không có text để đọc.');
            return {
                ok: false,
                mode: 'none',
                message: 'Không có text để đọc.'
            };
        }

        // Dừng hoàn toàn trước khi phát mới.
        self.stop();
        await new Promise(function (r) { setTimeout(r, 200); });
        self._speechStarted = false;
        self._speechError = '';

        var lang = (languageCode || 'vi').trim();

        // Tìm voice phù hợp nhất.
        var voices = await self._loadVoices();
        var voice =
            voices.find(function (v) { return v.lang === lang && v.localService; }) ||
            voices.find(function (v) { return v.lang === lang; }) ||
            voices.find(function (v) { return v.lang.startsWith(lang + '-'); }) ||
            voices.find(function (v) { return v.lang.startsWith(lang.split('-')[0]); }) ||
            null;

        if (voice) {
            console.log('[TTS] Voice:', voice.name, '|', voice.lang, '| local:', voice.localService);
        } else {
            console.warn('[TTS] Không có voice cho lang=' + lang + ', dùng default.');
            if (hasFallbackAudio) {
                var playedNoVoiceByAudio = await self._playAudioFallback(fallbackAudioUrl);
                if (playedNoVoiceByAudio) {
                    return {
                        ok: true,
                        mode: 'audio',
                        message: 'Thiết bị không có voice phù hợp, đã fallback sang audio.'
                    };
                }
            }
        }

        // Chia text thành câu ngắn và đưa vào queue.
        self._queue = self._splitSentences(normalizedText);
        self._isSpeaking = true;
        console.log('[TTS] Sẽ đọc', self._queue.length, 'câu. Lang:', lang);

        self._startHeartbeat();
        self._speakNext(voice, lang);

        // Mobile Chrome có thể nhận lệnh speak nhưng không phát âm thanh.
        // Nếu sau timeout vẫn chưa có onstart thì fallback audio để đảm bảo nghe được.
        var startedInTime = await new Promise(function (resolve) {
            var checkCount = 0;
            var maxChecks = 12; // ~3 giây
            var timer = setInterval(function () {
                checkCount++;
                if (self._speechStarted || (window.speechSynthesis && window.speechSynthesis.speaking)) {
                    clearInterval(timer);
                    resolve(true);
                    return;
                }

                if (checkCount >= maxChecks) {
                    clearInterval(timer);
                    resolve(false);
                }
            }, 250);
        });

        if (!startedInTime && hasFallbackAudio) {
            console.warn('[TTS] Speech không start trên mobile, chuyển sang audio fallback.');
            var playedAfterTimeout = await self._playAudioFallback(fallbackAudioUrl);
            if (!playedAfterTimeout) {
                var playedEmergency = await self._playEmergencyTtsAudio(normalizedText, languageCode);
                if (playedEmergency) {
                    return {
                        ok: true,
                        mode: 'audio',
                        message: 'Web Speech không start, audio URL lỗi; đã chuyển sang emergency cloud TTS.'
                    };
                }
            }

            return {
                ok: playedAfterTimeout,
                mode: playedAfterTimeout ? 'audio' : 'none',
                message: playedAfterTimeout
                    ? 'Web Speech không start trên thiết bị này, đã chuyển sang audio fallback.'
                    : ('Web Speech không start và audio fallback cũng không phát được. ' +
                        'AudioError=' + (self._lastAudioError || 'unknown') +
                        '. Vui lòng kiểm tra URL audio có public qua tunnel hay không.')
            };
        }

        if (!startedInTime) {
            var playedEmergencyNoAudioUrl = await self._playEmergencyTtsAudio(normalizedText, languageCode);
            if (playedEmergencyNoAudioUrl) {
                return {
                    ok: true,
                    mode: 'audio',
                    message: 'Web Speech không start, đã chuyển sang emergency cloud TTS.'
                };
            }

            return {
                ok: false,
                mode: 'none',
                message: self._speechError
                    ? 'Web Speech lỗi: ' + self._speechError
                    : 'Web Speech không start trên thiết bị này.'
            };
        }

        return {
            ok: true,
            mode: 'speech',
            message: 'Đang đọc bằng Web Speech API.'
        };
    },

    stop: function () {
        this._queue = [];
        this._isSpeaking = false;
        this._speechStarted = false;
        this._speechError = '';
        this._lastAudioError = '';
        this._stopHeartbeat();
        if (window.speechSynthesis) {
            window.speechSynthesis.cancel();
        }

        if (this._audioElement) {
            this._audioElement.pause();
            this._audioElement.currentTime = 0;
        }

        if (this._audioObjectUrl) {
            URL.revokeObjectURL(this._audioObjectUrl);
            this._audioObjectUrl = '';
        }

        console.log('[TTS] Đã dừng.');
    }
};
