using System;
using System.Drawing;
using System.Reflection;

// Force nested callbacks through the existing suppression predicate. Invalid HWNDs
// ensure these tests never disable an Explorer tooltip or launch a restore helper.
internal static class InfoTipSuppressorTests
{
    private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Assembly app, Action<string, Action> test)
    {
        Type type = app.GetType("DesktopHover.ShellInfoTipSuppressor", true);
        test("Infotip dismissal rejects a nested timer dismissal", delegate
        {
            object instance = null;
            int calls = 0;
            bool nested = true;
            instance = Create(type, delegate
            {
                if (++calls == 1) nested = Dismiss(type, instance);
                return false;
            });
            using ((IDisposable)instance)
            {
                Scope(type, instance);
                if (Dismiss(type, instance) || nested || calls != 1)
                    throw new Exception("A nested dismissal reached the suppression predicate.");
            }
        });
        test("Infotip timer dismissal rejects a nested window-show callback", delegate
        {
            object instance = null;
            int calls = 0;
            instance = Create(type, delegate
            {
                if (++calls == 1) ShowEvent(type, instance);
                return false;
            });
            using ((IDisposable)instance)
            {
                Scope(type, instance);
                Dismiss(type, instance);
                if (calls != 1) throw new Exception("Window-show callback re-entered timer suppression.");
            }
        });
        test("Infotip window-show callback still attempts a normal dismissal", delegate
        {
            int calls = 0;
            object instance = Create(type, delegate { calls++; return false; });
            using ((IDisposable)instance)
            {
                Scope(type, instance);
                ShowEvent(type, instance);
                if (calls != 1) throw new Exception("The normal window-show callback never reached suppression.");
                Dismiss(type, instance);
                if (calls != 2) throw new Exception("Window-show callback left suppression locked.");
            }
        });
        test("Infotip dismissal releases its guard after an exception", delegate
        {
            int calls = 0;
            object instance = Create(type, delegate
            {
                if (++calls == 1) throw new InvalidOperationException("test predicate failure");
                return false;
            });
            using ((IDisposable)instance)
            {
                Scope(type, instance);
                bool threw = false;
                try { Dismiss(type, instance); }
                catch (TargetInvocationException error)
                {
                    if (!(error.InnerException is InvalidOperationException)) throw;
                    threw = true;
                }
                if (!threw) throw new Exception("Expected predicate exception was not observed.");
                Dismiss(type, instance);
                if (calls != 2) throw new Exception("Suppression remained locked after an exception.");
            }
        });
        test("Infotip dismissal releases its guard after an early return", delegate
        {
            int calls = 0;
            object instance = Create(type, delegate { calls++; return false; });
            using ((IDisposable)instance)
            {
                Scope(type, instance);
                Dismiss(type, instance);
                Dismiss(type, instance);
                if (calls != 2) throw new Exception("Early return prevented later suppression attempts.");
            }
        });
        test("Clearing infotip scope during dismissal prevents later suppression", delegate
        {
            object instance = null;
            int calls = 0;
            instance = Create(type, delegate
            {
                calls++;
                type.GetMethod("Clear", HiddenInstance).Invoke(instance, null);
                return false;
            });
            using ((IDisposable)instance)
            {
                Scope(type, instance);
                Dismiss(type, instance);
                if ((IntPtr)type.GetField("root", HiddenInstance).GetValue(instance) != IntPtr.Zero ||
                    (IntPtr)type.GetField("view", HiddenInstance).GetValue(instance) != IntPtr.Zero)
                    throw new Exception("Clear retained a stale Explorer scope.");
                Dismiss(type, instance);
                if (calls != 1) throw new Exception("Cleared scope still reached suppression.");
            }
        });
        test("Infotip disposal is repeatable and prevents later callbacks", delegate
        {
            int calls = 0;
            object instance = Create(type, delegate { calls++; return false; });
            Scope(type, instance);
            ((IDisposable)instance).Dispose();
            ((IDisposable)instance).Dispose();
            Dismiss(type, instance);
            ShowEvent(type, instance);
            if (calls != 0) throw new Exception("Disposed suppressor still reached the predicate.");
        });
    }

    private static object Create(Type type, Func<bool> predicate)
    {
        return Activator.CreateInstance(type, HiddenInstance, null, new object[] { predicate }, null);
    }

    private static void Scope(Type type, object instance)
    {
        type.GetField("root", HiddenInstance).SetValue(instance, new IntPtr(1));
        type.GetField("view", HiddenInstance).SetValue(instance, new IntPtr(1));
    }

    private static bool Dismiss(Type type, object instance)
    {
        return (bool)type.GetMethod("DismissAtCursor", HiddenInstance).Invoke(instance,
            new object[] { new IntPtr(1), Point.Empty });
    }

    private static void ShowEvent(Type type, object instance)
    {
        type.GetMethod("OnWindowShown", HiddenInstance).Invoke(instance,
            new object[] { IntPtr.Zero, (uint)0x8002, new IntPtr(1), 0, 0, (uint)0, (uint)0 });
    }
}
