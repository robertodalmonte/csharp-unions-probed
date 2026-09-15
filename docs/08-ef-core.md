# 8. Persisting a union with EF Core

*Verified twice: on EF Core 10.0.9 / .NET 10 / SQLite with an abstract-record hierarchy, and on
EF Core `11.0.0-rc.1.26425.128` / SQLite with the real `union` keyword, using the code below
verbatim. Probe: `QEf`. The TPH defects were verified on EF Core 10.0.9.*

EF Core has no concept of a union, and none is scheduled. dotnet/efcore#36375 (*"Consider EF
support (**if any**)"*) is Backlog; the only milestoned item, dotnet/efcore#38059, is JSON-only,
EF 12, and notes that *"union types are structs, so they will have the same limitations in EF as
other structs."* Nothing in the EF 10 or 11 release notes mentions unions. So the technique is to
never make it EF's problem: map a flat DTO, bridge union↔DTO through a private property, and
`Ignore()` the union itself. The reusable asset is the bridge, not the keyword.

The worked example: a `Transfer` whose four-eyes `Approval` is one of five states.

```csharp
public union FourEyesApproval(NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected);

public record class NotRequired;
public record class PendingApproval;
public record class PartlyApproved(Guid Approver);
public record class FullyApproved(Guid Approver1, Guid Approver2);
public record class Rejected(Guid Rejector);
```

## The recipe

**1. A flat DTO**: one discriminator column plus nullable payload columns.

```csharp
public readonly record struct FourEyesApprovalData(   // struct: the bridge getter runs on every DetectChanges
    ApprovalKind Kind,                                  // enum, not a bare string
    Guid? Approver1,
    Guid? Approver2,
    Guid? Rejector);

// Reserve 0 for the invalid/uninitialized case (default(struct), or a raw INSERT that omits the
// column). ToModel throws on it instead of silently returning a valid business state.
public enum ApprovalKind { Unknown = 0, NotRequired, PendingApproval, PartlyApproved, FullyApproved, Rejected }
```

**2. Bidirectional conversion** (C# 14 `extension` members):

```csharp
public static class FourEyesApprovalConversions
{
    extension(FourEyesApproval approval)
    {
        public FourEyesApprovalData ToDataModel() => approval switch
        {
            NotRequired      => new(ApprovalKind.NotRequired,     null,        null,        null),
            PendingApproval  => new(ApprovalKind.PendingApproval, null,        null,        null),
            PartlyApproved p => new(ApprovalKind.PartlyApproved,  p.Approver,  null,        null),
            FullyApproved f  => new(ApprovalKind.FullyApproved,   f.Approver1, f.Approver2, null),
            Rejected r       => new(ApprovalKind.Rejected,        null,        null,        r.Rejector),
            // `null`, not `_`: covers default(FourEyesApproval) while keeping exhaustiveness.
            null => throw new InvalidOperationException("Transfer.Approval was never initialised."),
        };
    }

    extension(FourEyesApprovalData data)
    {
        // The corrupt-state boundary. `Kind` came from the store (an open set, so `_` is legitimate)
        // and the payload columns may disagree with it. Assert every field the case requires, with a
        // message that names what is missing.
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
```

`ToDataModel()` can live *on* the union (a union body may declare methods); `ToModel()` cannot,
because it converts *from* the DTO.

**3. The entity exposes the union and hides the DTO.** EF touches only the private property:

```csharp
public class Transfer(Guid id, Guid from, Guid to, FourEyesApproval approval)
{
    // EF placeholder constructor. Seed the neutral case, not a real business state. Defensive, not
    // load-bearing: EF sets the bridge before its first snapshot, so a default-seeded placeholder
    // also materializes (verified).
    private Transfer() : this(default, default, default, new NotRequired()) { }

    public Guid Id { get; private set; } = id;
    public Guid FromAccountId { get; private set; } = from;
    public Guid ToAccountId { get; private set; } = to;

    public FourEyesApproval Approval { get; private set; } = approval;

    // The bridge. The getter is called by EF at change detection and save; the setter on materialization.
    private FourEyesApprovalData ApprovalData
    {
        get => Approval.ToDataModel();
        set => Approval = value.ToModel();
    }

    public void WithApproval(FourEyesApproval approval) => Approval = approval;
}
```

**Mapping**: `Ignore` the union, map the DTO as a complex property.

```csharp
modelBuilder.Entity<Transfer>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.Ignore(x => x.Approval);                       // EF never sees the union
    entity.ComplexProperty<FourEyesApprovalData>("ApprovalData", b =>
    {
        // `Kind` maps to int by convention. Store it as a string only if you want the column
        // human-readable; this nested builder is where the conversion goes.
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
    });
});
```

Result columns on the owner table: `ApprovalData_Kind, ApprovalData_Approver1,
ApprovalData_Approver2, ApprovalData_Rejector`. No join, no second table.

### Why change tracking works

`Approval` is `Ignore()`d, so how does EF notice `WithApproval(...)`? The complex property's values
are snapshotted through the `ApprovalData` getter, which recomputes from `Approval`. Mutate the
union, the getter yields different component values, EF detects the change on the complex
property. You never tell EF about `Approval`; you tell it about the DTO the getter derives.

Verified on EF Core 11 RC 1 with the `union` keyword: materialization without `Include`,
`Modified` after `WithApproval` + `DetectChanges`, a `Partly → Fully → Rejected` sequence leaving
no stale payload columns, `WhereApproval` (below) translating to
`WHERE "t"."ApprovalData_Kind" = @kind`, and both `ToModel` corrupt-row messages.

### What the store's own checks catch, and what reaches `ToModel`

With a **string-mapped** `Kind`, EF rejects an unknown stored value itself, before `ToModel` runs:
*"Cannot convert string value 'Bogus' from the database to any value in the mapped 'ApprovalKind'
enum"*. So the `_ => throw` arm fires only for `Unknown`. With the conventional **int** mapping
(an `INTEGER` column), a stored `99` or `0` does reach `ToModel` and hits the arm: *"Corrupt
approval state: unknown kind '99'"* / *"… 'Unknown'"*.

## Four smaller traps

- **A stringly-typed discriminator is two sources of truth that drift.** A bare `string` in
  `nvarchar(max)`, duplicated across the two conversion switches, round-trips a typo until it
  doesn't. Use an `enum` so the case set is closed on both sides and a rename is a compile error.
- **Column overloading is the price of this flattening.** `PartlyApproved.Approver` and
  `FullyApproved.Approver1` both land in `ApprovalData_Approver1`. Accept it and comment the
  mapping; or map the DTO to a single JSON column (`ComplexProperty(...).ToJson()`), which is
  unaffected by the union's JSON problems because what goes to JSON is the flat DTO with an
  explicit `Kind`, not the union; or use TPH, below, and pay for it differently.
- **Enum ordinal 0 is a silent default.** If a real case sits at 0, an uninitialized
  `default(FourEyesApprovalData)` or a raw `INSERT` that omits the column reads back as that valid
  business state. Reserve 0 for `Unknown`. This holds for int and string mappings alike.
- **The bridge getter runs on every `DetectChanges`.** If the DTO is a `record` class, every call
  heap-allocates a throwaway object. Make it a `readonly record struct`.

## Querying

You cannot filter on `Approval`; it is ignored. Query the mapped DTO, and hide the magic string
in one place:

```csharp
extension(IQueryable<Transfer> transfers)
{
    public IQueryable<Transfer> WhereApproval(ApprovalKind kind) => transfers.Where(t =>
        EF.Property<FourEyesApprovalData>(t, "ApprovalData").Kind == kind);
}

var transfer = dbContext.Transfers.WhereApproval(ApprovalKind.PartlyApproved).FirstOrDefault();
if (transfer?.Approval is PartlyApproved p)
    transfer.WithApproval(new FullyApproved(p.Approver, secondApprover));
```

## The TPH alternative, and why it loses for mutable state

The other shape in circulation: make the case hierarchy a real EF entity hierarchy with a
discriminator and table-split it into the owner's table (`HasOne(t => t.Approval).WithOne()
.HasForeignKey<FourEyesApproval>("Id")`, `ToTable("Transfers")`, `HasDiscriminator<string>(…)`,
`Navigation(...).IsRequired().AutoInclude()`). It buys SQL translation of `t.Approval is
PartlyApproved`, one named column per case property, and no conversion code. It costs three
things, the first two verified on EF 10.0.9:

1. **Superseded cases leave their columns populated.** Transitioning `PartlyApproved →
   FullyApproved` emits `UPDATE Transfers SET ApprovalType=…, Approver1=…, Approver2=… WHERE Id=…`.
   EF writes the new type's properties and never nulls the old type's columns. The row ends up
   `ApprovalType=FullyApproved` with `Approver` still holding the previous approver. This is
   dotnet/efcore#36308, closed without a code fix. The bridge cannot have this defect: the getter
   rewrites all payload columns on every save.
2. **Miss `Include` and the invariant silently evaporates.** `db.Transfers.First(...)` without
   `.Include(t => t.Approval)` returns a `Transfer` whose non-nullable `Approval` is `null`,
   despite `Navigation(...).IsRequired()`. `AutoInclude()` closes it. The bridge stores state as
   columns on the row, so it is always materialized.
3. **It relies on unsupported behaviour twice over.** Changing a tracked entity's type is
   officially rejected (dotnet/efcore#7509); the transition doesn't throw only because
   dotnet/efcore#7340 lets a deleted and an added entity share a key, and #36308 is the corruption
   that falls out of it. TPH on a table-split dependent is documented only by omission.

Choose TPH only when case payloads never change after the row is written. Otherwise use the
bridge.

Note that this is a union that lives entirely in the domain; the persisted shape is the DTO. That
is also the answer to the union's JSON problems ([04](04-system-text-json.md)): the boundary sees
the DTO's explicit `Kind`, never the union.
