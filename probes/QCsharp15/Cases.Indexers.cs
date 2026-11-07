// Extension indexers. Claims from section 4 of the skill.
public static partial class Cases
{
    public static readonly Case[] Indexers =
    [
        new("E01", "extension(IEnumerable<int> sequence) { public int this[int index] } works: numbers[2] == 3",
            Wrap("""
                IEnumerable<int> numbers = Enumerable.Range(1, 10);
                Console.WriteLine(numbers[2]);
                """, """
                public static class SequenceIndexer
                {
                    extension(IEnumerable<int> sequence)
                    {
                        public int this[int index] => sequence.ElementAt(index);
                    }
                }
                """)),

        new("E02", "Two-parameter get/set extension indexer on IDictionary<string,string>",
            Wrap("""
                IDictionary<string, string> settings = new Dictionary<string, string>();
                settings["Logging", "Level"] = "Debug";
                Console.WriteLine($"{settings["Logging", "Level"]} | {string.Join(",", settings.Keys)}");
                """, """
                public static class SettingsExtensions
                {
                    extension(IDictionary<string, string> settings)
                    {
                        public string this[string section, string key]
                        {
                            get => settings[$"{section}:{key}"];
                            set => settings[$"{section}:{key}"] = value;
                        }
                    }
                }
                """)),

        new("E03", "The block needs a NAMED receiver: extension(IEnumerable<int>) with an indexer is an error",
            Wrap("", """
                public static class Bad
                {
                    extension(IEnumerable<int>)
                    {
                        public int this[int index] => 0;
                    }
                }
                """)),

        new("E04", "An extension can never hijack an instance indexer: List<int>[0] stays the real element",
            Wrap("""
                var l = new List<int> { 5 };
                IList<int> il = l;
                Console.WriteLine($"{l[0]} {il[0]}");
                """, """
                public static class Hijack
                {
                    extension(List<int> list)
                    {
                        public int this[int index] => -1;
                    }
                }
                """)),

        new("E05", "Extension indexers are never used for string or arrays: \"abc\"[1] and arr[0] stay real (this[int] declared on both)",
            Wrap("""
                int[] arr = [7, 8];
                Console.WriteLine($"{"abc"[1]} {arr[0]}");
                """, """
                public static class NoHijack
                {
                    extension(string s) { public char this[int index] => 'X'; }
                    extension(int[] a) { public int this[int index] => -1; }
                }
                """)),

        new("E06", "...but a DIFFERENT signature on string is usable (\"abc\"[\"k\"]): the claim 'never used for string' is only about colliding shapes",
            Wrap("""
                Console.WriteLine("abc"["k"]);
                int[] arr = [7, 8];
                Console.WriteLine(arr["k"]);
                """, """
                public static class OtherShape
                {
                    extension(string s) { public string this[string key] => s + ":" + key; }
                    extension(int[] a) { public string this[string key] => a.Length + ":" + key; }
                }
                """)),

        new("E07", "Cannot appear in expression trees (the lambda s => s[2] to Expression<>)",
            Wrap("""
                Expression<Func<IEnumerable<int>, int>> e = s => s[2];
                """, """
                public static class SequenceIndexer
                {
                    extension(IEnumerable<int> sequence) { public int this[int index] => sequence.ElementAt(index); }
                }
                """)),

        new("E08", "Extension Count + this[int] + Slice(int,int) unlock ^1, ranges and list patterns",
            Wrap("""
                Readings r = new([10, 20, 30, 40]);
                Console.WriteLine($"{r[^1]} {r[1..3][0]} {r is [10, .., 40]}");
                """, """
                public sealed class Readings(int[] values) { public int[] Values { get; } = values; }
                public static class ReadingExtensions
                {
                    extension(Readings readings)
                    {
                        public int Count => readings.Values.Length;
                        public int this[int index] => readings.Values[index];
                        public Readings Slice(int start, int length) => new(readings.Values.AsSpan(start, length).ToArray());
                    }
                }
                """)),

        new("E09", "[IndexerName(\"X\")] changes the metadata names; default is Item (reflected static method names)",
            Wrap("""
                foreach (var t in new[] { typeof(Named), typeof(Plain) })
                    Console.WriteLine($"{t.Name}: " + string.Join(", ", t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly).Select(m => m.Name).OrderBy(n => n)));
                Console.WriteLine(Named.get_X(new Dictionary<string, string> { ["a:b"] = "v" }, "a", "b"));
                Console.WriteLine(Plain.get_Item(new Dictionary<string, string> { ["a:b"] = "v" }, "a", "b"));
                """, """
                public static class Named
                {
                    extension(IDictionary<string, string> s)
                    {
                        [IndexerName("X")]
                        public string this[string a, string b] => s[$"{a}:{b}"];
                    }
                }
                public static class Plain
                {
                    extension(IDictionary<string, string> s)
                    {
                        public string this[string a, string b] => s[$"{a}:{b}"];
                    }
                }
                """)),

        new("E10", "Forbidden modifiers on an extension indexer (one block each, one line each)",
            Wrap("", """
                public static class M1 { extension(List<int> l) { public virtual int this[string k] => 0; } }
                public static class M2 { extension(List<int> l) { public abstract int this[string k] { get; } } }
                public static class M3 { extension(List<int> l) { public override int this[string k] => 0; } }
                public static class M4 { extension(List<int> l) { public new int this[string k] => 0; } }
                public static class M5 { extension(List<int> l) { public sealed int this[string k] => 0; } }
                public static class M6 { extension(List<int> l) { protected int this[string k] => 0; } }
                public static class M7 { extension(List<int> l) { public int this[string k] { get => 0; init { } } } }
                public static class M8 { extension(List<int> l) { public partial int this[string k] { get; } } }
                public static class M9 { extension(List<int> l) { public static int this[string k] => 0; } }
                """)),

        new("E11", "A type parameter of a generic extension block must appear in the receiver or the indexer parameters",
            Wrap("", """
                public static class G1 { extension<T>(List<int> l) { public int this[string k] => 0; } }
                public static class G2 { extension<T>(List<int> l) { public int this[T k] => 0; } }
                public static class G3 { extension<T>(List<T> l) { public int this[string k] => 0; } }
                """)),

        new("E12", "Type inference uses receiver and index args only, never the assigned value: i[42, null] = new object() fails, E.set_Item(...) succeeds",
            Wrap("""
                var i = new Holder();
                E.set_Item(i, 42, null, new object());
                Console.WriteLine("explicit set_Item ok");
                i[42, "s"] = "v";
                Console.WriteLine("T from the index argument ok");
                i[42, null] = new object();
                """, """
                public class Holder { }
                public static class E
                {
                    extension<T>(Holder h)
                    {
                        public T this[int index, T hint] { get => default!; set { } }
                    }
                }
                """)),

        new("E13", "Null-conditional x?[i,j] and index assignment inside an object initializer both resolve to the extension indexer",
            Wrap("""
                var d = new Dictionary<string, int> { ["ab"] = 1 };
                Console.WriteLine(d["a", "b"]);
                Dictionary<string, int>? none = null;
                Console.WriteLine(none?["a", "b"] is null ? "null" : "value");
                var init = new Dictionary<string, int> { ["x", "y"] = 5 };
                Console.WriteLine(string.Join(",", init.Select(kv => kv.Key + "=" + kv.Value)));
                """, """
                public static class DictIx
                {
                    extension(Dictionary<string, int> d)
                    {
                        public int this[string a, string b] { get => d[a + b]; set => d[a + b] = value; }
                    }
                }
                """)),

        new("E14", "ref-returning extension indexer: w[1] = 7; w[1]++",
            Wrap("""
                var w = new Wrap([1, 2, 3]);
                w[1] = 7;
                w[1]++;
                Console.WriteLine(string.Join(",", w.Arr));
                """, """
                public sealed class Wrap(int[] arr) { public int[] Arr { get; } = arr; }
                public static class WrapIx
                {
                    extension(Wrap w)
                    {
                        public ref int this[int index] => ref w.Arr[index];
                    }
                }
                """)),

        new("E15", "Lookup order tier 2 beats tier 3: implicit Index support from INSTANCE members (Length + this[int]) beats an extension this[Index]",
            Wrap("""
                var t = new Inst();
                Console.WriteLine($"{t[1]} {t[^1]}");
                """, """
                public sealed class Inst
                {
                    public int Length => 3;
                    public string this[int i] => "instance" + i;
                }
                public static class InstIx
                {
                    extension(Inst t) { public string this[Index i] => "extension-Index"; }
                }
                """)),

        new("E15b", "Tier 3 beats tier 4: an extension this[Index] beats implicit Index support from extension Count + this[int]",
            Wrap("""
                var t = new Bare();
                Console.WriteLine($"{t[1]} {t[^1]}");
                """, """
                public sealed class Bare { }
                public static class BareIx
                {
                    extension(Bare t)
                    {
                        public int Count => 3;
                        public string this[int i] => "ext-int" + i;
                        public string this[Index i] => "ext-Index";
                    }
                }
                """)),

        new("E16", "Two extension indexers with the same receiver and signature in different classes are ambiguous (which diagnostic?)",
            Wrap("""
                var h = new Holder();
                Console.WriteLine(h[1]);
                """, """
                public class Holder { }
                public static class A { extension(Holder h) { public int this[int i] => 1; } }
                public static class B { extension(Holder h) { public int this[int i] => 2; } }
                """)),

        new("E16b", "...control: with one of them removed, the same call compiles",
            Wrap("""
                var h = new Holder();
                Console.WriteLine(h[1]);
                """, """
                public class Holder { }
                public static class A { extension(Holder h) { public int this[int i] => 1; } }
                """)),

        new("E17", "A NON-colliding extension indexer on a type that already has an instance indexer: List<int>[\"k\"] and Dictionary<string,int>[1]",
            Wrap("""
                var l = new List<int> { 5 };
                Console.WriteLine(l["k"]);
                """, """
                public static class Mixed
                {
                    extension(List<int> l) { public string this[string key] => "ext:" + key + l.Count; }
                }
                """)),

        new("E17b", "...and on string / array with a non-colliding signature (the E06 shape, one line each)",
            Wrap("""
                Console.WriteLine("abc"["k"]);
                """, """
                public static class Mixed
                {
                    extension(string s) { public string this[string key] => "ext:" + key + s.Length; }
                }
                """)),
    ];
}
