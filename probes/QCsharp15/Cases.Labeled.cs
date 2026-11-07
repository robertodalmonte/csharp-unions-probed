// Labeled break / continue. Claims from section 5 of the skill.
public static partial class Cases
{
    public static readonly Case[] Labeled =
    [
        new("L01", "break outer / continue outer in for, foreach, while and do loops",
            Wrap("""
                var hit = (-1, -1);
                outer: for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 5; c++)
                    {
                        if (c == 1) continue outer;
                        if (r == 3) break outer;
                        hit = (r, c);
                    }
                }
                Console.WriteLine($"for: {hit}");

                int n = 0;
                w: while (true)
                {
                    foreach (var x in new[] { 1, 2, 3 })
                    {
                        n += x;
                        if (n > 8) break w;
                        if (x == 2) continue w;
                    }
                }
                Console.WriteLine($"while/foreach: {n}");

                int k = 0;
                d: do
                {
                    k++;
                    for (int i = 0; i < 3; i++) { if (i == 1) continue d; if (k > 3) break d; }
                } while (k < 100);
                Console.WriteLine($"do: {k}");
                """)),

        new("L02", "break dispatch; inside a foreach inside a switch STATEMENT exits the switch, not just the foreach",
            Wrap("""
                dispatch: switch (1)
                {
                    case 1:
                        foreach (var step in new[] { 1, 2, 3 })
                        {
                            if (step == 2) break dispatch;
                            Console.WriteLine($"step {step}");
                        }
                        Console.WriteLine("after foreach (skipped if the switch exited)");
                        break;
                    default:
                        break;
                }
                Console.WriteLine("after switch");
                """)),

        new("L03", "continue label cannot target a switch",
            Wrap("""
                dispatch: switch (1)
                {
                    case 1:
                        for (int i = 0; i < 2; i++) continue dispatch;
                        break;
                }
                """)),

        new("L04", "Only the statement immediately after a label is labeled: a: b: while ... break a is an error",
            Wrap("""
                a: b: while (true)
                {
                    break a;
                }
                """)),

        new("L05", "...and break b works (control)",
            Wrap("""
                a: b: while (true)
                {
                    break b;
                }
                Console.WriteLine("left via b");
                """)),

        new("L06", "break label to a label on a plain block or an if is an error",
            Wrap("""
                blk: { for (int i = 0; i < 2; i++) { break blk; } }
                """)),

        new("L07", "break outer across try/finally runs the finally blocks, innermost first",
            Wrap("""
                outer: for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        try
                        {
                            for (int j = 0; j < 2; j++) { Console.WriteLine("jump"); break outer; }
                        }
                        finally { Console.WriteLine("inner finally"); }
                    }
                    finally { Console.WriteLine("outer finally"); }
                }
                Console.WriteLine("after");
                """)),

        new("L08", "Neither form can leave a finally block",
            Wrap("""
                outer: for (int i = 0; i < 2; i++)
                {
                    try { }
                    finally { break outer; }
                }
                """)),

        new("L09", "Cannot jump out of a lambda or a local function to an outer loop (one line each)",
            Wrap("""
                outer: for (int i = 0; i < 2; i++)
                {
                    Action a = () => { break outer; };
                    void Local() { continue outer; }
                }
                """)),

        new("L10", "The same label on two nested loops is an error; give each its own name",
            Wrap("""
                outer: for (int i = 0; i < 2; i++)
                {
                    outer: for (int j = 0; j < 2; j++) { break outer; }
                }
                """)),

        new("L11", "The same label on two SEQUENTIAL loops in one block is an error (label scope is the whole block)",
            Wrap("""
                outer: for (int i = 0; i < 2; i++) { break outer; }
                outer: for (int j = 0; j < 2; j++) { break outer; }
                """)),

        new("L12", "...but the same label in sibling blocks is fine",
            Wrap("""
                { outer: for (int i = 0; i < 2; i++) { break outer; } }
                { outer: for (int j = 0; j < 2; j++) { break outer; } }
                Console.WriteLine("ok");
                """)),

        new("L13", "One label serves both break and continue, replacing the goto pair",
            Wrap("""
                int skipped = 0, taken = 0;
                rows: for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        if (c == 2) { skipped++; continue rows; }
                        if (r == 3) break rows;
                        taken++;
                    }
                }
                Console.WriteLine($"taken={taken} skipped={skipped}");
                """)),

        new("L14", "The old goto to the same label still compiles, and a labeled loop is still a goto target",
            Wrap("""
                int i = 0;
                again: while (i < 3)
                {
                    i++;
                    if (i == 2) { i = 10; goto again; }
                }
                Console.WriteLine(i);
                """)),

        new("L15", "Labeled jumps inside iterator and async methods",
            Wrap("""
                Console.WriteLine(string.Join(",", Gen()));
                Console.WriteLine(Asy().GetAwaiter().GetResult());
                """, "", """
                static IEnumerable<int> Gen()
                {
                    outer: for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            if (j == 1) continue outer;
                            if (i == 3) break outer;
                            yield return i * 10 + j;
                        }
                    }
                }
                static async Task<int> Asy()
                {
                    int s = 0;
                    outer: for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            await Task.Yield();
                            if (j == 2) continue outer;
                            if (i == 2) break outer;
                            s += 1;
                        }
                    }
                    return s;
                }
                """)),

        new("L16", "Unlabeled break/continue unchanged: a break in a nested loop leaves only the inner loop",
            Wrap("""
                int n = 0;
                for (int i = 0; i < 3; i++) { for (int j = 0; j < 3; j++) { if (j == 1) break; n++; } }
                Console.WriteLine(n);
                """)),

        new("L17", "Label name that equals a local variable name, and a label never used (warning?)",
            Wrap("""
                int outer = 0;
                outer: for (int i = 0; i < 2; i++) { outer++; }
                unused: for (int i = 0; i < 2; i++) { }
                Console.WriteLine(outer);
                """)),
    ];
}
