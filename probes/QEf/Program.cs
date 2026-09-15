using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

if (File.Exists("qef.db")) File.Delete("qef.db");
Guid id = Guid.NewGuid(), a1 = Guid.NewGuid(), a2 = Guid.NewGuid(), r1 = Guid.NewGuid(), badId = Guid.NewGuid();

await using (var db = new Ctx())
{
    await db.Database.EnsureCreatedAsync();
    Console.WriteLine("DDL: " + Scalar("SELECT sql FROM sqlite_master WHERE name='Transfers'")?.ReplaceLineEndings(" "));
    db.Transfers.Add(new Transfer(id, Guid.NewGuid(), Guid.NewGuid(), new PartlyApproved(a1)));
    db.BadTransfers.Add(new BadTransfer(badId, new PendingApproval()));
    await db.SaveChangesAsync();
}
Console.WriteLine("after insert (Partly):   " + Row());

await using (var db = new Ctx())
{
    var t = await db.Transfers.FirstAsync(x => x.Id == id);
    Console.WriteLine($"materialized, no Include: {Show(t.Approval)}; state={db.Entry(t).State}");
    t.WithApproval(new FullyApproved(a1, a2));
    db.ChangeTracker.DetectChanges();
    Console.WriteLine($"after WithApproval + DetectChanges: state={db.Entry(t).State}");
    await db.SaveChangesAsync();
}
Console.WriteLine("after Partly -> Fully:   " + Row());

await using (var db = new Ctx())
{
    var t = await db.Transfers.FirstAsync(x => x.Id == id);
    t.WithApproval(new Rejected(r1));
    await db.SaveChangesAsync();
}
Console.WriteLine("after Fully -> Rejected: " + Row());

await using (var db = new Ctx())
{
    var q = db.Transfers.WhereApproval(ApprovalKind.Rejected);
    Console.WriteLine("SQL: " + q.ToQueryString().ReplaceLineEndings(" "));
    Console.WriteLine($"WhereApproval(Rejected)={await q.CountAsync()}, WhereApproval(PartlyApproved)={await db.Transfers.WhereApproval(ApprovalKind.PartlyApproved).CountAsync()}");
}

try
{
    await using var db = new Ctx();
    var b = await db.BadTransfers.FirstAsync(x => x.Id == badId);
    Console.WriteLine($"BadTransfer (placeholder ctor seeds default union) materialized: {Show(b.Approval)}; state={db.Entry(b).State}");
    db.ChangeTracker.DetectChanges();
    Console.WriteLine($"BadTransfer after DetectChanges: state={db.Entry(b).State}");
}
catch (Exception e) { Console.WriteLine($"BadTransfer materialize THROWS {e.GetType().Name}: {e.Message}"); }

await Probe("BadTransfer AsNoTracking", async () => { await using var db = new Ctx(); return Show((await db.BadTransfers.AsNoTracking().FirstAsync(x => x.Id == badId)).Approval); });
await Probe("BadTransfer FindAsync", async () => { await using var db = new Ctx(); return Show((await db.BadTransfers.FindAsync(badId))!.Approval); });
await Probe("Add(new Transfer(..., default)), no SaveChanges", async () => { await using var db = new Ctx(); var e = db.Transfers.Add(new Transfer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default)); return $"added, state={e.State}"; });
await Probe("Attach(new Transfer(..., default)) [placeholder seeds NotRequired]", async () => { await using var db = new Ctx(); db.Transfers.Attach(new Transfer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default)); return "attached"; });
await Probe("Update(new Transfer(..., default)) [placeholder seeds NotRequired]", async () => { await using var db = new Ctx(); db.Transfers.Update(new Transfer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default)); return "updated"; });
await Probe("Attach(new BadTransfer(..., default)) [placeholder seeds default]", async () => { await using var db = new Ctx(); db.BadTransfers.Attach(new BadTransfer(Guid.NewGuid(), default)); return "attached"; });

try
{
    await using var db = new Ctx();
    db.Transfers.Add(new Transfer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default));
    await db.SaveChangesAsync();
    Console.WriteLine("Add with default(FourEyesApproval): SAVED");
}
catch (Exception e) { Console.WriteLine($"Add with default(FourEyesApproval) THROWS {e.GetType().Name}: {e.Message}"); }

Exec("UPDATE Transfers SET ApprovalData_Kind = 'PartlyApproved', ApprovalData_Approver1 = NULL");
try
{
    await using var db = new Ctx();
    var t = await db.Transfers.FirstAsync();
    Console.WriteLine($"corrupt row (Partly, Approver1 NULL) loaded: {Show(t.Approval)}");
}
catch (Exception e) { Console.WriteLine($"corrupt row (Partly, Approver1 NULL) THROWS {e.GetType().Name}: {e.Message}"); }

Exec("UPDATE Transfers SET ApprovalData_Kind = 'Bogus'");
try
{
    await using var db = new Ctx();
    var t = await db.Transfers.FirstAsync();
    Console.WriteLine($"corrupt row (Kind='Bogus') loaded: {Show(t.Approval)}");
}
catch (Exception e) { Console.WriteLine($"corrupt row (Kind='Bogus') THROWS {e.GetType().Name}: {e.Message}"); }

if (File.Exists("qef-int.db")) File.Delete("qef-int.db");
await using (var db = new IntCtx())
{
    await db.Database.EnsureCreatedAsync();
    db.Transfers.Add(new Transfer(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new PartlyApproved(a1)));
    await db.SaveChangesAsync();
}
Console.WriteLine("int-mapped Kind column: " + ScalarOn("qef-int.db", "SELECT type FROM pragma_table_info('Transfers') WHERE name='ApprovalData_Kind'"));
foreach (var k in new[] { "99", "0" })
{
    ExecOn("qef-int.db", $"UPDATE Transfers SET ApprovalData_Kind = {k}");
    await Probe($"int-mapped Kind={k} loaded", async () => { await using var db = new IntCtx(); return Show((await db.Transfers.FirstAsync()).Approval); });
}

static async Task Probe(string label, Func<Task<string>> f)
{
    try { Console.WriteLine($"{label} => {await f()}"); }
    catch (Exception e) { Console.WriteLine($"{label} => THROWS {e.GetType().Name}: {e.Message}"); }
}

static void ExecOn(string file, string sql)
{
    using var c = new SqliteConnection($"Data Source={file}");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
}

static string? ScalarOn(string file, string sql)
{
    using var c = new SqliteConnection($"Data Source={file}");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    return cmd.ExecuteScalar()?.ToString();
}

static string Show(FourEyesApproval a) => a.Value is null ? "Value=null" : a.Value.ToString()!;

static string? Scalar(string sql)
{
    using var c = new SqliteConnection("Data Source=qef.db");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    return cmd.ExecuteScalar()?.ToString();
}

static void Exec(string sql)
{
    using var c = new SqliteConnection("Data Source=qef.db");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = sql;
    cmd.ExecuteNonQuery();
}

static string Row()
{
    using var c = new SqliteConnection("Data Source=qef.db");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = "SELECT ApprovalData_Kind, ApprovalData_Approver1, ApprovalData_Approver2, ApprovalData_Rejector FROM Transfers";
    using var r = cmd.ExecuteReader();
    r.Read();
    return string.Join(" | ", Enumerable.Range(0, 4).Select(i => r.IsDBNull(i) ? "NULL" : r.GetString(i)[..Math.Min(8, r.GetString(i).Length)]));
}

// ---- verbatim from discriminated-unions/SKILL.md ----
public union FourEyesApproval(
    NotRequired,
    PendingApproval,
    PartlyApproved,
    FullyApproved,
    Rejected);

public record class NotRequired;
public record class PendingApproval;
public record class PartlyApproved(Guid Approver);
public record class FullyApproved(Guid Approver1, Guid Approver2);
public record class Rejected(Guid Rejector);

public readonly record struct FourEyesApprovalData(
    ApprovalKind Kind,
    Guid? Approver1,
    Guid? Approver2,
    Guid? Rejector);

public enum ApprovalKind { Unknown = 0, NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected }

public static class FourEyesApprovalConversions
{
    extension(FourEyesApproval approval)
    {
        public FourEyesApprovalData ToDataModel() => approval switch
        {
            NotRequired      => new(ApprovalKind.NotRequired,     null,          null,       null),
            PendingApproval  => new(ApprovalKind.PendingApproval, null,          null,       null),
            PartlyApproved p => new(ApprovalKind.PartlyApproved,  p.Approver,    null,       null),
            FullyApproved f  => new(ApprovalKind.FullyApproved,   f.Approver1,   f.Approver2,null),
            Rejected r       => new(ApprovalKind.Rejected,        null,          null,       r.Rejector),
            null => throw new InvalidOperationException("Transfer.Approval was never initialised."),
        };
    }

    extension(FourEyesApprovalData data)
    {
        public FourEyesApproval ToModel() => data.Kind switch
        {
            ApprovalKind.NotRequired     => new NotRequired(),
            ApprovalKind.PendingApproval => new PendingApproval(),
            ApprovalKind.PartlyApproved  => new PartlyApproved(data.Require(data.Approver1, nameof(data.Approver1))),
            ApprovalKind.FullyApproved   => new FullyApproved(
                                                data.Require(data.Approver1, nameof(data.Approver1)),
                                                data.Require(data.Approver2, nameof(data.Approver2))),
            ApprovalKind.Rejected        => new Rejected(data.Require(data.Rejector, nameof(data.Rejector))),
            _ => throw new InvalidOperationException($"Corrupt approval state: unknown kind '{data.Kind}'."),
        };

        private Guid Require(Guid? value, string column) =>
            value ?? throw new InvalidOperationException(
                $"Corrupt approval state: Kind={data.Kind} requires column '{column}', but it was null.");
    }
}

public class Transfer(Guid id, Guid from, Guid to, FourEyesApproval approval)
{
    private Transfer() : this(default, default, default, new NotRequired()) { }

    public Guid Id { get; private set; } = id;
    public Guid FromAccountId { get; private set; } = from;
    public Guid ToAccountId { get; private set; } = to;

    public FourEyesApproval Approval { get; private set; } = approval;

    private FourEyesApprovalData ApprovalData
    {
        get => Approval.ToDataModel();
        set => Approval = value.ToModel();
    }

    public void WithApproval(FourEyesApproval approval) => Approval = approval;
}

public static class TransferQueries
{
    extension(IQueryable<Transfer> transfers)
    {
        public IQueryable<Transfer> WhereApproval(ApprovalKind kind) => transfers.Where(t =>
            EF.Property<FourEyesApprovalData>(t, "ApprovalData").Kind == kind);
    }
}
// ---- end verbatim ----

// Trap 6 probe: the placeholder ctor the skill forbids — seeds default(FourEyesApproval)
public class BadTransfer(Guid id, FourEyesApproval approval)
{
    private BadTransfer() : this(default, default) { }
    public Guid Id { get; private set; } = id;
    public FourEyesApproval Approval { get; private set; } = approval;
    private FourEyesApprovalData ApprovalData
    {
        get => Approval.ToDataModel();
        set => Approval = value.ToModel();
    }
}

public class IntCtx : DbContext
{
    public DbSet<Transfer> Transfers => Set<Transfer>();
    protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=qef-int.db");
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Transfer>(e =>
    {
        e.HasKey(x => x.Id);
        e.Ignore(x => x.Approval);
        e.ComplexProperty<FourEyesApprovalData>("ApprovalData");   // Kind maps to int by convention
    });
}

public class Ctx : DbContext
{
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<BadTransfer> BadTransfers => Set<BadTransfer>();

    protected override void OnConfiguring(DbContextOptionsBuilder o) => o.UseSqlite("Data Source=qef.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.Approval);
            entity.ComplexProperty<FourEyesApprovalData>("ApprovalData", b =>
            {
                b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            });
        });
        modelBuilder.Entity<BadTransfer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Ignore(x => x.Approval);
            entity.ComplexProperty<FourEyesApprovalData>("ApprovalData", b =>
            {
                b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            });
        });
    }
}
