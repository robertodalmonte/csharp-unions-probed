// Section 0 / 8 claims (version check, breaking changes) and the section 7 memory-safety preview.
public static partial class Cases
{
    public static readonly Case[] Misc =
    [
        new("M01", "Section 0: '#error version' reports CS8304 with compiler and language versions",
            "#error version\npublic static class P { public static void Run() { } }\n"),

        new("M02", "Section 8: a loop-local Span<int> items = [x] assigned to an outer span is now an error",
            Wrap("""
                Span<int> outer = default;
                for (int i = 0; i < 2; i++)
                {
                    Span<int> items = [i];
                    outer = items;
                }
                """)),

        new("M03", "Section 8: nameof(this.P) inside an attribute is now an error; nameof(P) is fine",
            Wrap("", """
                public class Marker(string n) : Attribute { }
                public class Holder
                {
                    public int P;
                    [Marker(nameof(this.P))] public int Q;
                    [Marker(nameof(P))] public int R;
                }
                """)),

        new("M04", "Section 8: x && y with x an interface type and y dynamic is CS7083",
            Wrap("""
                IDisposable? x = null;
                dynamic y = 1;
                var z = x && y;
                """)),

        new("M05", "Section 8: a type named 'closed' needs @closed (CS9380); a field of type closed may give CS1519",
            Wrap("", """
                public class closed { }
                public class UsesIt { public closed Field; }
                """)),

        new("M06", "Section 8: 'union Foo;' inside a type now parses as a union declaration (CS9370); @union escapes it",
            Wrap("", """
                public class union { }
                public class Host { union Foo; }
                public class Host2 { @union Foo2; }
                """)),

        new("M07", "Section 8: (X.Y) when ... in a switch-expression arm parses as constant pattern + when, not a cast",
            Wrap("""
                object o = 1;
                int r = o switch { (E.A) when true => 1, _ => 2 };
                Console.WriteLine(r);
                """, "public enum E { A }")),

        new("U01", "Preview: a requires-unsafe member called from safe code is an error with the updated rules",
            Wrap("", "", """
                public static unsafe int ReadInt32(byte* source) { unsafe { return *(int*)source; } }
                public static void Caller(byte[] b) { fixed (byte* p = b) { var v = ReadInt32(p); } }
                """), Preview: true, AllowUnsafe: true),

        new("U02", "Preview: the same call inside unsafe { } compiles",
            Wrap("", "", """
                public static unsafe int ReadInt32(byte* source) { unsafe { return *(int*)source; } }
                public static void Caller(byte[] b) { fixed (byte* p = b) { unsafe { var v = ReadInt32(p); } } }
                """), Preview: true, AllowUnsafe: true),

        new("U03", "Preview: 'unsafe' on a signature no longer opens an unsafe context for the body (dereference without inner block)",
            Wrap("", "", """
                public static unsafe int Deref(int* p) { return *p; }
                """), Preview: true, AllowUnsafe: true),

        new("U04", "Preview, ORIGINAL rules (no feature flag): the same unsafe method body is an unsafe context",
            Wrap("", "", """
                public static unsafe int Deref(int* p) { return *p; }
                """), Preview: true, AllowUnsafe: true, OriginalRules: true),

        new("U05", "Preview: SumBytes discharges the obligation: pinned array, inner unsafe block, safe signature",
            Wrap("""
                Console.WriteLine(SumBytes([1, 2, 3]));
                """, "", """
                public static int SumBytes(byte[] source)
                {
                    ArgumentNullException.ThrowIfNull(source);
                    fixed (byte* first = source)
                    {
                        unsafe
                        {
                            int total = 0;
                            for (int i = 0; i < source.Length; i++) total += first[i];
                            return total;
                        }
                    }
                }
                """), Preview: true, AllowUnsafe: true),

        new("U06", "Preview: unsafe(expr) in a field initializer calls a requires-unsafe member from a field initializer (no block possible)",
            Wrap("""
                Console.WriteLine(Signature);
                """, "", """
                static int Value = 42;
                static unsafe int ReadValue() { unsafe { fixed (int* p = &Value) { return *p; } } }
                static readonly int Signature = unsafe(ReadValue());
                """), Preview: true, AllowUnsafe: true),

        new("U06b", "...control: the same field initializer without unsafe(...) is CS9362",
            Wrap("", "", """
                static int Value = 42;
                static unsafe int ReadValue() { unsafe { fixed (int* p = &Value) { return *p; } } }
                static readonly int Signature = ReadValue();
                """), Preview: true, AllowUnsafe: true),

        new("U07", "Preview: unsafe on a type declaration, a static constructor, a finalizer and a lambda are errors (one line each)",
            Wrap("", """
                public unsafe class T1 { }
                public class T2 { static unsafe T2() { } }
                public class T3 { unsafe ~T3() { } }
                public class T4 { public void M() { Action a = unsafe () => { }; } }
                """, ""), Preview: true, AllowUnsafe: true),

        new("U08", "Preview: await is allowed inside an unsafe block but not inside a fixed statement",
            Wrap("", "", """
                public static async Task A(byte[] b) { unsafe { await Task.Yield(); } }
                public static async Task B(byte[] b) { fixed (byte* p = b) { await Task.Yield(); } }
                """), Preview: true, AllowUnsafe: true),

        new("U09", "Preview: every extern method must say safe or unsafe; a bare extern is an error (one line each)",
            Wrap("", """
                public static class Native
                {
                    [System.Runtime.InteropServices.DllImport("libc")] internal static extern int Bare();
                    [System.Runtime.InteropServices.DllImport("libc")] internal static safe extern int Safe();
                    [System.Runtime.InteropServices.DllImport("libc")] internal static unsafe extern nint Strlen(byte* s);
                }
                """, ""), Preview: true, AllowUnsafe: true),

        new("U13", "Preview: instance fields of an explicit-layout struct need safe/unsafe (CS9392); safe resolves it",
            Wrap("", """
                [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
                public struct S1 { [System.Runtime.InteropServices.FieldOffset(0)] public int A; }
                [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
                public struct S2 { [System.Runtime.InteropServices.FieldOffset(0)] public safe int A; }
                """, ""), Preview: true, AllowUnsafe: true),

        new("U10", "Preview: modifiers 'safe'/'unsafe' without AllowUnsafeBlocks (CS9401 per the skill; CS0227 for unsafe(expr))",
            Wrap("", "", """
                public static unsafe int X(int* p) { unsafe { return *p; } }
                static readonly int Y = unsafe(1);
                """), Preview: true, AllowUnsafe: false),

        new("U11", "Preview WITHOUT the feature flag: pointer relaxations only (declare a pointer, &local, fixed, sizeof) compile in safe code",
            Wrap("", "", """
                public static void M(int[] a) { int n = 4; int* p = &n; fixed (int* q = a) { } var s = sizeof(int); }
                """), Preview: true, AllowUnsafe: false, OriginalRules: true),

        new("U12", "...and WITHOUT preview the same lines are CS0214",
            Wrap("", "", """
                public static void M(int[] a) { int n = 4; int* p = &n; fixed (int* q = a) { } var s = sizeof(int); }
                """)),
    ];
}
