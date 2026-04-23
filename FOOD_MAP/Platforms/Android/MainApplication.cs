using Android.App;
using Android.Runtime;
using Android.Util;
using System.Runtime.ExceptionServices;

namespace FOOD_MAP
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            RegisterGlobalExceptionHandlers();
            base.OnCreate();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        private static void RegisterGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception exception)
                {
                    LogException("AppDomain.UnhandledException", exception);
                }
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogException("TaskScheduler.UnobservedTaskException", args.Exception);
                args.SetObserved();
            };

            AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            {
                LogException("AndroidEnvironment.UnhandledExceptionRaiser", args.Exception);
            };
        }

        private static void LogException(string source, Exception exception)
        {
            var flattened = exception.FlattenIfAggregate();
            Log.Error("FOOD_MAP", $"{source}: {flattened}");

            var inner = flattened.InnerException;
            while (inner is not null)
            {
                Log.Error("FOOD_MAP", $"Inner: {inner}");
                inner = inner.InnerException;
            }
        }
    }

    internal static class ExceptionExtensions
    {
        public static Exception FlattenIfAggregate(this Exception exception)
        {
            return exception is AggregateException aggregate
                ? aggregate.Flatten().InnerException ?? aggregate
                : exception;
        }
    }
}
