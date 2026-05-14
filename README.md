# PRODUCT REQUIREMENTS DOCUMENT: FOOD MAP

## 1. OVERVIEW

**FOOD MAP** is a smart tourism map application that helps visitors easily find, navigate, and interact with Points of Interest (POIs) via audio narration (TTS), QR code scanning, and food menu browsing. The system consists of:

- A cross-platform mobile application built with **.NET MAUI** (Android primary target).
- A shared library **FOOD_MAP.Shared** for data models, DbContext, and reusable Razor UI components.
- A **Blazor Server Web Admin (CMS)** for managing POI content, translations, subscriptions, and monitoring.
- A **Remote PostgreSQL** database shared by both the mobile app and the web.

**Project Goals:**
- Digitize on-site tourism experiences (Auto TTS narration, QR scanning, GPS-based POI alerts, food menus).
- Operate stably even under unstable network conditions (Offline Queue Sync).
- Support multilingual content through a language ownership and contributor mechanism.
- Provide a Subscription/Payment model for Owner-tier accounts with feature unlocking.

---

## 2. SYSTEM ARCHITECTURE

The project is built on **.NET MAUI** and a **Remote PostgreSQL** database following a Clean Architecture pattern (Repository Pattern), with support for background sync when connectivity is lost.

![Architecture Diagram](FOOD_MAP/Resources/Images/architecture_diagram.png)

| Layer | Component | Responsibility |
| --- | --- | --- |
| **Presentation** | MainTabsPage, HomePage, PoiPage, CameraPage, MapPage, LoginPage, RegisterPage, SettingsPage | UI rendering and user interaction |
| **ViewModel** | MainPageViewModel, SettingsViewModel, LoginViewModel | UI business logic, Data binding |
| **Service** | AuthService, NarrationService, DataService, MobileApiServices, ActiveMobileHeartbeatAgent | Core application logic and cross-cutting features |
| **Repository** | PoiRepository, UserActivityRepository, SyncQueue | Data access and Offline Queue management |
| **Data** | AppDbContext (EF Core + Npgsql), Remote PostgreSQL | Persistent data layer |

---

## 3. SYSTEM ACTORS

The system serves a diverse set of user types through 4 main roles:

| Actor | Description | Permissions |
| --- | --- | --- |
| **Guest** | Anonymous visitor | View the POI map, scan QR codes, listen to TTS narration, view food menus. Cannot save data. |
| **User** | Registered & logged-in | All Guest permissions + Save Favorites, record Tour History, update Profile, upgrade Subscription. |
| **Owner** | Admin-approved business operator | All User permissions + Create/Edit own POIs, manage Restaurant Menus, request language translation rights, purchase subscription plans. |
| **Admin** | System administrator | Full access: Approve/Reject POIs, approve Owners, manage language ownership, manage all Subscription plans and transactions. |

---

## 4. FUNCTIONAL STRUCTURE & LIFECYCLE

The system is branched and built around 3 core data types: **User Identity, Location Data (POI), and Food Menu Data**.

### 4.1 Dependency Tree

This diagram illustrates why the absence of the 3 root entities (User, POI, FoodItem) prevents all symbiotic features (Subscription, QR, Offline Sync) from functioning.

![Dependency Tree Model](FOOD_MAP/Resources/Images/dependency_tree.png)

### 4.2 Product Experience Lifecycle

The activity flow diagram shows data circulation in real-time across 18 standardized use case processes — from content preparation by the admin team to the moment a tourist uses the app on-site.

![Product Lifecycle Activity](FOOD_MAP/Resources/Images/lifecycle_activity.png)

---

## 5. DATA MODEL (ERD)

The entity relationship diagram for the project with Remote PostgreSQL.

![Entity Relationship Diagram](FOOD_MAP/Resources/Images/data_model_diagram.png)

| Table | Description | Purpose |
| --- | --- | --- |
| **Users** | User accounts | Authentication, role management |
| **POIs** | Points of interest / restaurants | Core geolocation and general data |
| **POITranslations** | Translated content | Multi-language name, description, TTS script, audio URL |
| **Languages** | Supported languages | Language catalog for translation |
| **FoodItems** | Individual food items | Linked per POI for restaurant menus |
| **TourList** | Ordered POI lists | A curated sequence of POIs forming a tour itinerary |
| **UserTours** | Tour history log | Tracks user visits per POI with timestamp |
| **UserFavorites** | Favorites log | Tracks user-favorited POIs |
| **MediaAssets** | Media file metadata | Cached image/audio file references per translation |
| **SyncHistories** | Sync log | Records background sync events and errors |
| **SubscriptionPlans** | Subscription tiers | Defines feature limits and fixed pricing (Admin-managed) |
| **PaymentTransactions** | Payment records | Logs every payment with a unique TransactionCode |

---

## 6. ACTIVITY DIAGRAMS — 18 Features

<details>
<summary><b>Click to expand all 18 Activity Diagrams</b></summary>

### 6.1 Login / Register / Continue as Guest
![act_01_login](FOOD_MAP/Resources/Images/act_01_login.png)

### 6.2 View POI Map
![act_02_viewmap](FOOD_MAP/Resources/Images/act_02_viewmap.png)

### 6.3 Search for a Location
![act_03_search](FOOD_MAP/Resources/Images/act_03_search.png)

### 6.4 Play TTS Narration
![act_04_tts](FOOD_MAP/Resources/Images/act_04_tts.png)

### 6.5 Scan QR Code
![act_05_qr](FOOD_MAP/Resources/Images/act_05_qr.png)

### 6.6 GPS Navigation & Tour Recording
![act_06_gps](FOOD_MAP/Resources/Images/act_06_gps.png)

### 6.7 Mark as Favorite
![act_07_favorite](FOOD_MAP/Resources/Images/act_07_favorite.png)

### 6.8 Change Language
![act_08_language](FOOD_MAP/Resources/Images/act_08_language.png)

### 6.9 View Food Menu
![act_09_menu](FOOD_MAP/Resources/Images/act_09_menu.png)

### 6.10 Manage Profile
![act_10_profile](FOOD_MAP/Resources/Images/act_10_profile.png)

### 6.11 Register as a Business Owner
![act_11_ownerrequest](FOOD_MAP/Resources/Images/act_11_ownerrequest.png)

### 6.12 Manage POI (Owner)
![act_12_managepoi](FOOD_MAP/Resources/Images/act_12_managepoi.png)

### 6.13 Manage Food Menu (Food Manager)
![act_13_foodmanager](FOOD_MAP/Resources/Images/act_13_foodmanager.png)

### 6.14 Request Translation Language Rights
![act_14_langownership](FOOD_MAP/Resources/Images/act_14_langownership.png)

### 6.15 Admin Approval Workflow
![act_15_admin](FOOD_MAP/Resources/Images/act_15_admin.png)

### 6.16 Offline Data Auto-Sync
![act_16_offlinesync](FOOD_MAP/Resources/Images/act_16_offlinesync.png)

### 6.17 Subscription Plan Management
![act_17_subscription](FOOD_MAP/Resources/Images/act_17_subscription.png)

### 6.18 Active User Tracking
![act_18_usertracking](FOOD_MAP/Resources/Images/act_18_usertracking.png)

</details>

---

## 7. SEQUENCE DIAGRAMS — 18 Features

<details>
<summary><b>Click to expand all 18 Sequence Diagrams</b></summary>

### 7.1 Login / Register
![seq_01_login](FOOD_MAP/Resources/Images/seq_01_login.png)

### 7.2 View POI Map
![seq_02_viewmap](FOOD_MAP/Resources/Images/seq_02_viewmap.png)

### 7.3 Search for a Location
![seq_03_search](FOOD_MAP/Resources/Images/seq_03_search.png)

### 7.4 Play TTS Narration
![seq_04_tts](FOOD_MAP/Resources/Images/seq_04_tts.png)

### 7.5 Scan QR Code
![seq_05_qr](FOOD_MAP/Resources/Images/seq_05_qr.png)

### 7.6 GPS Navigation & Tour Recording
![seq_06_gps](FOOD_MAP/Resources/Images/seq_06_gps.png)

### 7.7 Mark as Favorite
![seq_07_favorite](FOOD_MAP/Resources/Images/seq_07_favorite.png)

### 7.8 Change Language
![seq_08_language](FOOD_MAP/Resources/Images/seq_08_language.png)

### 7.9 View Food Menu
![seq_09_menu](FOOD_MAP/Resources/Images/seq_09_menu.png)

### 7.10 Manage Profile
![seq_10_profile](FOOD_MAP/Resources/Images/seq_10_profile.png)

### 7.11 Register as a Business Owner
![seq_11_ownerrequest](FOOD_MAP/Resources/Images/seq_11_ownerrequest.png)

### 7.12 Manage POI (Owner)
![seq_12_managepoi](FOOD_MAP/Resources/Images/seq_12_managepoi.png)

### 7.13 Manage Food Menu (Food Manager)
![seq_13_foodmanager](FOOD_MAP/Resources/Images/seq_13_foodmanager.png)

### 7.14 Request Translation Language Rights
![seq_14_langownership](FOOD_MAP/Resources/Images/seq_14_langownership.png)

### 7.15 Admin Approval Workflow
![seq_15_admin](FOOD_MAP/Resources/Images/seq_15_admin.png)

### 7.16 Offline Data Auto-Sync
![seq_16_offlinesync](FOOD_MAP/Resources/Images/seq_16_offlinesync.png)

### 7.17 Subscription Plan Management
![seq_17_subscription](FOOD_MAP/Resources/Images/seq_17_subscription.png)

### 7.18 Active User Tracking
![seq_18_usertracking](FOOD_MAP/Resources/Images/seq_18_usertracking.png)

</details>

---

## 8. STATE TRANSITIONS

### 8.1 POI Lifecycle States
| From State | Action | To State |
| --- | --- | --- |
| (new) | Owner submits a POI | Pending |
| Pending | Admin reviews and approves | Approved |
| Pending | Admin rejects (invalid info) | Rejected |

### 8.2 Owner Verification States
| From State | Action | To State |
| --- | --- | --- |
| (new) | User submits business registration | Pending |
| Pending | Admin verifies and approves | Approved + Role: Owner |

### 8.3 Offline Sync Queue States
| State | Description |
| --- | --- |
| Queued | Operation saved locally, waiting for connectivity. |
| Processing | HTTP request in progress with retry policy. |
| Flushed | Successfully sent to server, local record cleared. |
| Retry | HTTP timeout occurred, re-queued with exponential backoff (2^n). |

### 8.4 Subscription Payment States
| From State | Action | To State |
| --- | --- | --- |
| (new) | Owner selects a plan and initiates payment | Pending |
| Pending | Payment Provider confirms transaction | Active |
| Active | Billing period expires (checked by cron job) | Expired |

---

## 9. KEY TECHNICAL RULES

### Subscription & Billing
- Subscription plan prices are **fixed** and visible to Owners as read-only.
- **Only Admins** can create, modify, or delete subscription plans.
- Each payment generates a **unique, non-repeating `TransactionCode`** stored in `PaymentTransactions`.

### Role-Based UI Visibility (Web CMS)
- **Guests / Users**: Can only access the public `PoiScanner` page (view POI info, play TTS in browser).
- **Owners**: Access CMS to manage their own POIs, view Heatmaps, and make payments. Cannot view active user counts.
- **Admins**: Full access to all CMS pages including the real-time active user dashboard.

### Deep Linking
- Scanning a QR code with a native phone camera opens the MAUI app directly if installed.
- Falls back to the `PoiScanner` web page if the app is not installed.

### Real-time Active User Tracking
- Implemented via SignalR/WebSockets.
- Users become `inactive` within **1 second** of logging out, closing the app, or losing internet.
- This metric is **exclusively visible to Admins**.

### Environment & Security
- All sensitive data (DB connection strings, API keys, payment secrets) must be stored in `.env` files.
- `.env` files are **excluded from version control** via `.gitignore`.
- Passwords are stored as salted hashes — never plain text.
