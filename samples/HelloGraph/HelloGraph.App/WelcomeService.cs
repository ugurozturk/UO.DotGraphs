using HelloGraph.Domain;

namespace HelloGraph.App;

public sealed class WelcomeService
{
    public string Welcome(string name) => new Greeter().Greet(name);
}
