using Polly;

namespace Workleap.DomainEventPropagation.Analyzers.Tests;

public class EventDomainAttributeUsageAnalyzerTests : BaseAnalyzerTest<EventDomainAttributeUsageAnalyzer>
{
    [Fact]
    public async Task Given_NoAttribute_When_Analyze_Then_Diagnostics()
    {
        const string source = """
public class {|WLDEP01:SampleDomainEvent|} : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_DomainEventAttribute_When_Analyze_Then_No_Diagnostic()
    {
        const string source = """
[DomainEvent("com.workleap.domainservice.created")]
public class SampleDomainEvent : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_Struct_With_DomainEventAttribute_When_Analyze_Then_No_Diagnostic()
    {
        const string source = """
[DomainEvent("com.workleap.domainservice.created")]
public record SampleDomainEvent : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_Random_Attribute_When_Analyze_Then_Diagnostics()
    {
        const string source = """
[Serializable]
public class {|WLDEP01:SampleDomainEvent|} : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_DomainEventAttribute_With_No_Interface_When_Analyze_Then_No_Diagnostic()
    {
        const string source = """
[DomainEvent("com.workleap.domainservice.created")]
public class SampleDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_DomainEventAttribute_With_Multiple_Attributes_When_Analyze_Then_No_Diagnostic()
    {
        const string source = """
[DomainEvent("com.workleap.domainservice.created")]
[Serializable]
public class SampleDomainEvent : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_DomainEventAttribute_With_Non_Unique_Value_When_Analyze_Then_Diagnostic()
    {
        const string source = """
[DomainEvent("com.workleap.domainservice.created")]
public class {|WLDEP02:SampleDomainEvent|} : IDomainEvent
{    
}
[DomainEvent("com.workleap.domainservice.created")]
public class {|WLDEP02:SampleDomainEvent2|} : IDomainEvent
{    
}
""";
        await new BaseAnalyzerTest<EventDomainAttributeUsageAnalyzer>()
            .WithSourceCode(source)
            .RunAsync();
    }

    [Fact]
    public async Task Given_DomainEventAttribute_With_Unique_Value_When_Analyze_Then_No_Diagnostic()
    {
        const string source = """
[DomainEvent("com.workleap.domainservice.created")]
public class SampleDomainEvent : IDomainEvent
{    
}
[DomainEvent("com.workleap.domainservice.updated")]
public class SampleDomainEvent2 : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Theory]
    [InlineData("com.workleap.domainservice.created")]
    [InlineData("com.workleap.domainservice.entity.created")]
    [InlineData("com.workleap.domainservice.entity.level.sublevel.created")]
    public async Task Given_DomainEventAttribute_With_Value_In_Reverse_Dns_Convention_Analyze_Then_No_Diagnostic(string eventName)
    {
        var source = $$"""
[DomainEvent("{{eventName}}")]
public class SampleDomainEvent : IDomainEvent
{    
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }

    [Theory]
    [InlineData("comsampledomainserviceevent")] // no periods in event name
    [InlineData("com.invalidProduct.domainservice.created")] // invalid product name
    [InlineData("com.workleap.DOMAINservice.created")] // capital letters in event name
    [InlineData("com.workleap.created")] // missing segments
    [InlineData("com.workleap.domainservice..created")] // double period in event name
    [InlineData("net.workleap.domainservice.entity.created")] // not starting with com
    public async Task Given_DomainEventAttribute_With_Value_In_Reverse_Dns_Convention_Analyze_Then_Diagnostic(string eventName)
    {
        var source = $$"""
[DomainEvent(name: {|WLDEP03:"{{eventName}}"|})]
public class SampleDomainEvent : IDomainEvent
{
}
""";

        await this.WithSourceCode(source)
            .RunAsync();
    }
}