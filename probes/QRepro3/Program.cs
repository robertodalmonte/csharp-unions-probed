Pet real = new Dog("Rex");
Pet none = default;
#if RC1_IS_UNION_TYPE // as posted; CS8121 from RC 2 on
Console.WriteLine(real is Pet); // True
Console.WriteLine(none is Pet); // True
#endif

Pet pet = default;
try
{
    var name = pet switch { Dog d => d.Name, Cat c => c.Name, Bird b => b.Name };  // no warning
    Console.WriteLine(name);
}
catch (Exception e) { Console.WriteLine(e.GetType().FullName); }

public record class Cat(string Name);
public record class Dog(string Name);
public record class Bird(string Name);
public union Pet(Cat, Dog, Bird);
