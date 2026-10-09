using System;
using System.Reflection;

namespace SemanticFixture;
public interface IRoot { void Run(int value); }
public interface IContract : IRoot { int Value { get; set; } }
public class Base
{
    public virtual void Virtual() { }
    public virtual int Number { get; set; }
}
public partial class Worker : Base, IContract
{
    public Worker() : this(1) { }
    public Worker(int value) { Value = value; }
    public int Value { get; set; }
    public override int Number { get; set; }
    public override void Virtual() { }
    void IRoot.Run(int value) { }
    public void Run(string value) { }
    public void Overload(int value) { }
    public void Overload(string value) { }
    public T Generic<T>(T value) => value;
    public void Callback() { }
    public void LambdaOnly() { }
    public void LocalOnly() { }
    public void Calls(IContract contract, Base polymorphic)
    {
        Overload(1);
        Overload(2);
        Overload("text");
        Generic<int>(3);
        this.Extend();
        contract.Run(1);
        polymorphic.Virtual();
        base.Virtual();
        new Worker(1).Virtual();
        Value = 1;
        var read = Value;
        Value++;
        Action group = Callback;
        group();
        Action lambda = () => LambdaOnly();
        lambda();
        void Local() => LocalOnly();
        Local();
        Console.WriteLine(read);
    }
    public void Unknown(dynamic value) => value.Anything();
    public void Reflected(MethodInfo method) => method.Invoke(this, null);
}
public static class Extensions
{
    public static void Extend(this Worker worker) { }
}
#if BROKEN_SCENARIO
public partial class Worker
{
    public void Broken()
    {
        Overload(new object());
        Generic<Missing>(null);
        new Worker("wrong");
        MissingCall();
        Overload(1); // Recovery in an erroneous body is conservatively unresolved too.
    }
    public void Ambiguous() => Choose(null);
    public void Choose(Uri value) { }
    public void Choose(Version value) { }
}
#endif
