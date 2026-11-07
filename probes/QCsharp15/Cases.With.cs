// Collection expression arguments: [with(...), ...]. Claims from section 1 of the skill.
public static partial class Cases
{
    const string MySetTypes = """
        [CollectionBuilder(typeof(MySetBuilder), nameof(MySetBuilder.Create))]
        public sealed class MySet<T> : IEnumerable<T>
        {
            private readonly HashSet<T> _items;
            internal MySet(ReadOnlySpan<T> items, IEqualityComparer<T>? comparer = null)
            {
                _items = new HashSet<T>(comparer);
                foreach (var item in items) _items.Add(item);
            }
            public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public static class MySetBuilder
        {
            public static MySet<T> Create<T>(ReadOnlySpan<T> items) => new(items);
            public static MySet<T> Create<T>(IEqualityComparer<T> comparer, ReadOnlySpan<T> items) => new(items, comparer);
        }
        """;

    const string SpanFirstTypes = """
        [CollectionBuilder(typeof(BadBuilder), nameof(BadBuilder.Create))]
        public sealed class BadSet<T> : IEnumerable<T>
        {
            private readonly List<T> _items = [];
            internal BadSet(ReadOnlySpan<T> items, IEqualityComparer<T>? comparer) { foreach (var i in items) _items.Add(i); }
            public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public static class BadBuilder
        {
            public static BadSet<T> Create<T>(ReadOnlySpan<T> items, IEqualityComparer<T>? comparer = null) => new(items, comparer);
        }
        """;

    const string NamedValuesTypes = """
        public class NamedValues(string name) : List<int>
        {
            public string Name { get; } = name;
        }
        """;

    public static readonly Case[] With =
    [
        new("W01", "List<T> capacity: [with(capacity: n), ..xs] presizes",
            Wrap("""
                List<int> l = [with(capacity: 8), 1, 2];
                Console.WriteLine($"{l.Count} {l.Capacity}");
                string[] src = ["a", "b"];
                List<string> n = [with(capacity: src.Length * 2), .. src];
                Console.WriteLine($"{n.Count} {n.Capacity}");
                """)),

        new("W02", "HashSet comparer: [with(OrdinalIgnoreCase), \"Hello\", \"HELLO\", \"hello\"] has 1 element",
            Wrap("""
                HashSet<string> set = [with(StringComparer.OrdinalIgnoreCase), "Hello", "HELLO", "hello"];
                Console.WriteLine(set.Count);
                """)),

        new("W03", "IList<T>: [with(capacity: 100)] creates List<int>(100); ICollection<T> takes with() and with(capacity)",
            Wrap("""
                IList<int> a = [with(capacity: 100), 1, 2, 3];
                Console.WriteLine($"{a.GetType().Name} cap={((List<int>)a).Capacity}");
                IList<int> b = [with(), 1];
                ICollection<int> c = [with(capacity: 4), 1];
                ICollection<int> d = [with(), 1];
                Console.WriteLine($"{b.GetType().Name} {c.GetType().Name} {d.GetType().Name} cap(c)={((List<int>)c).Capacity}");
                """)),

        new("W04", "List<int> copy = [with([1, 2]), 3] binds List<T>(IEnumerable<T>) -> 1, 2, 3",
            Wrap("""
                List<int> copy = [with([1, 2]), 3];
                Console.WriteLine(string.Join(",", copy));
                """)),

        new("W05", "Builder type: Create<T>(IEqualityComparer<T>, ReadOnlySpan<T>) -> with(comparer) gives 2 elements; with() calls Create(ROS)",
            Wrap("""
                MySet<string> s = [with(StringComparer.OrdinalIgnoreCase), "A", "a", "B"];
                MySet<string> t = [with(), "x"];
                MySet<string> u = ["p", "P"];
                Console.WriteLine($"{s.Count()} {t.Count()} {u.Count()}");
                """, MySetTypes)),

        new("W06", "A type whose only constructor takes arguments is a target WITH with(...)",
            Wrap("""
                NamedValues readings = [with("temperature"), 18, 21, 24];
                Console.WriteLine($"{readings.Name}: {string.Join(",", readings)}");
                """, NamedValuesTypes)),

        new("W07", "...and is an error WITHOUT with(...) (no parameterless constructor)",
            Wrap("""
                NamedValues bad = [18, 21, 24];
                """, NamedValuesTypes)),

        new("W08", "with(...) not first is a compile error",
            Wrap("""
                List<int> l = [1, with(capacity: 3)];
                """)),

        new("W09", "Arrays and spans: an error even for an empty with() (one line each)",
            Wrap("""
                int[] a = [with(), 1];
                Span<int> s = [with(), 1];
                ReadOnlySpan<int> r = [with(), 1];
                """)),

        new("W10", "IEnumerable<T>, IReadOnlyCollection<T>, IReadOnlyList<T> accept only an empty with()",
            Wrap("""
                IEnumerable<int> a = [with(), 1];
                IReadOnlyCollection<int> b = [with(), 1];
                IReadOnlyList<int> c = [with(), 1];
                Console.WriteLine($"{a.GetType().Name} {b.GetType().Name} {c.GetType().Name}");
                """)),

        new("W11", "...and with(capacity: 10) on them is an error (one line each)",
            Wrap("""
                IEnumerable<int> a = [with(capacity: 10), 1];
                IReadOnlyCollection<int> b = [with(capacity: 10), 1];
                IReadOnlyList<int> c = [with(capacity: 10), 1];
                """)),

        new("W12", "Overload resolution ignores the arguments: Print(List<string>)/Print(HashSet<string>) with [with(comparer), ...] is CS0121",
            Wrap("""
                Print([with(StringComparer.OrdinalIgnoreCase), "one", "ONE"]);
                """, "", "static void Print(List<string> values) { }\nstatic void Print(HashSet<string> values) { }")),

        new("W13", "...but a fixed target type then resolves it",
            Wrap("""
                HashSet<string> h = [with(StringComparer.OrdinalIgnoreCase), "one", "ONE"];
                Print(h);
                Console.WriteLine(h.Count);
                """, "", "static void Print(List<string> values) { }\nstatic void Print(HashSet<string> values) { }")),

        new("W14", "Inference ignores the arguments: Use([with(10)]) is CS0411; Use<int>([with(10)]) is fine",
            Wrap("""
                Use([with(10)]);
                Use<int>([with(10)]);
                """, "", "static void Use<T>(List<T> values) { }")),

        new("W15", "[with(default)] on List<int> is ambiguous between constructors",
            Wrap("""
                List<int> l = [with(default)];
                """)),

        new("W16", "dynamic arguments are an error",
            Wrap("""
                dynamic cap = 5;
                List<int> l = [with(cap), 1];
                """)),

        new("W17", "Evaluation order: with-args first, then elements left to right; spread call happens in position, enumeration later",
            Wrap("""
                List<string> log = [];
                int A(string s) { log.Add(s); return 4; }
                IEnumerable<int> Items() { log.Add("enumerate"); yield return 1; }
                IEnumerable<int> Call() { log.Add("call"); return Items(); }
                List<int> r = [with(capacity: A("with")), A("e1"), .. Call(), A("e2")];
                Console.WriteLine(string.Join(", ", log));
                """)),

        new("W18", "A method named 'with' is called as an element via @with(...) (element position)",
            Wrap("""
                List<int> l = [@with(1, 2), 3];
                Console.WriteLine(string.Join(",", l));
                """, "", "static int with(int a, int b) => a + b;")),

        new("W19", "...and unescaped with(1, 2) at element 0 is collection arguments, so List<int>(1, 2) fails",
            Wrap("""
                List<int> l = [with(1, 2), 3];
                """, "", "static int with(int a, int b) => a + b;")),

        new("W20", "Dictionary targets: empty with(comparer) only? (one line each)",
            Wrap("""
                Dictionary<string, int> d = [with(StringComparer.OrdinalIgnoreCase)];
                d["A"] = 1;
                Console.WriteLine(d.ContainsKey("a"));
                IDictionary<string, int> i = [with(StringComparer.OrdinalIgnoreCase)];
                IReadOnlyDictionary<string, int> r = [with()];
                """)),

        new("W21", "Builder with the span FIRST and an optional comparer after: with(comparer) cannot bind; plain and with() can",
            Wrap("""
                BadSet<string> a = ["x"];
                BadSet<string> b = [with(), "x"];
                BadSet<string> c = [with(StringComparer.Ordinal), "x"];
                Console.WriteLine($"{a.Count()} {b.Count()}");
                """, SpanFirstTypes)),

        new("W22", "Capacity vs copy: [with(5)] on List<int> is capacity 5",
            Wrap("""
                List<int> l = [with(5)];
                Console.WriteLine($"{l.Count} {l.Capacity}");
                """)),

        new("W23", "ImmutableArray<T>/ImmutableHashSet<T>/SortedSet<T> builders and comparers",
            Wrap("""
                ImmutableArray<int> a = [with(), 1, 2];
                ImmutableHashSet<string> h = [with(StringComparer.OrdinalIgnoreCase), "x", "X"];
                SortedSet<string> s = [with(StringComparer.Ordinal), "b", "a"];
                Console.WriteLine($"{a.Length} {h.Count} {s.First()}");
                """)),

        new("W24", "A second with(...) element is an error",
            Wrap("""
                List<int> l = [with(capacity: 1), with(capacity: 2), 3];
                """)),

        new("W26", "Spread enumeration order by target kind: List, HashSet, builder type, ImmutableArray, array (log of when the iterator runs vs later elements)",
            Wrap("""
                List<string> log = [];
                int A(string s) { log.Add(s); return 1; }
                IEnumerable<int> Items() { log.Add("enumerate"); yield return 1; }
                IEnumerable<int> Call() { log.Add("call"); return Items(); }
                void Show(string label) { Console.WriteLine($"{label}: {string.Join(", ", log)}"); log.Clear(); }

                List<int> a = [with(capacity: 4), A("e1"), .. Call(), A("e2")]; Show("List");
                HashSet<int> b = [with(), A("e1"), .. Call(), A("e2")]; Show("HashSet");
                MySet<int> c = [with(), A("e1"), .. Call(), A("e2")]; Show("MySet(builder)");
                ImmutableArray<int> d = [with(), A("e1"), .. Call(), A("e2")]; Show("ImmutableArray");
                int[] e = [A("e1"), .. Call(), A("e2")]; Show("int[] (no with)");
                IEnumerable<int> f = [A("e1"), .. Call(), A("e2")]; Show("IEnumerable<int>");
                """, MySetTypes)),

        new("W25","Classic initializers are unchanged; with(...) is only a collection-expression feature",
            Wrap("""
                var l = new List<int>(capacity: 4) { 1, 2 };
                Console.WriteLine($"{l.Count} {l.Capacity}");
                """)),
    ];
}
