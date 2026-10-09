namespace Fixture;
public interface IContract { void Run(int value); }
public partial class Box<T> : IContract
{
    public Box() { }
    public Box(T value) { Value = value; }
    static Box() { }
    public T? Value { get; set; }
    public int Field;
    public event System.Action? Changed;
    public int this[int index] => index;
    public void Run(int value) => Changed?.Invoke();
    public void Run(string value) { }
    void IContract.Run(int value) { }
    public void Generic<U>(U value) { }
    public void Generic<U, V>(U value, V other) { }
    public int Locals()
    {
        int Read(int value) => value;
        { int Same() => 1; Field += Same(); }
        { int Same() => 2; Field += Same(); }
        return Read(Field);
    }
    public class Nested<U> { }
    public static Box<T> operator +(Box<T> left, Box<T> right) => left;
    public static implicit operator int(Box<T> value) => value.Field;
    public static explicit operator string(Box<T> value) => value.Field.ToString();
}
public class Box { }
public static class Extensions
{
    public static int Length<T>(this Box<T> box) => box.Field;
}
public enum Color { Red, Blue }
public delegate void Callback(int value);
#if NET10_0
public class Net10Only { }
#else
public class StandardOnly { }
#endif
#if RELEASE_FEATURE
public class ReleaseOnly { }
#else
public class DebugOnly { }
#endif

public class Primary(int value) { public int Value => value; }
