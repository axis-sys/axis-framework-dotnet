using Axis.Analyzers;

namespace AxisBus.Analyzers.UnitTests;

public class PublishAfterSaveChangesAnalyzerTests
{
    private const string Preamble =
        """
        using System;
        using System.Threading.Tasks;
        using Axis;
        using AxisMediator.Contracts.CQRS.Events;
        public record PingEvent : IAxisEvent;
        public interface IUow { Task<AxisResult> SaveChangesAsync(); }
        """;

    [Fact]
    public async Task FlagsPublishAfterSaveInStatementsAsync()
    {
        var source = Preamble +
            """
            class C
            {
                async Task<AxisResult> M(IAxisBus bus, IUow uow)
                {
                    await uow.SaveChangesAsync();
                    return await bus.PublishAsync(new PingEvent());
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0300"));
    }

    // Fluent form of the same bug: the commit runs first, the publish enqueues on a drained queue.
    [Fact]
    public async Task FlagsPublishChainedAfterSaveAsync()
    {
        var source = Preamble +
            """
            class C
            {
                Task<AxisResult> M(IAxisBus bus, IUow uow)
                    => uow.SaveChangesAsync()
                        .ThenAsync(() => bus.PublishAsync(new PingEvent()));
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(1, diagnostics.Count("AXIS0300"));
    }

    [Fact]
    public async Task IgnoresPublishBeforeSaveInStatementsAsync()
    {
        var source = Preamble +
            """
            class C
            {
                async Task<AxisResult> M(IAxisBus bus, IUow uow)
                {
                    await bus.PublishAsync(new PingEvent());
                    return await uow.SaveChangesAsync();
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0300"));
    }

    // The canonical shape: publish enqueues, the commit passed as a METHOD GROUP drains it.
    [Fact]
    public async Task IgnoresPublishChainedBeforeMethodGroupSaveAsync()
    {
        var source = Preamble +
            """
            class C
            {
                Task<AxisResult> M(IAxisBus bus, IUow uow)
                    => bus.PublishAsync(new PingEvent())
                        .ThenAsync(uow.SaveChangesAsync);
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0300"));
    }

    // No commit in the member — it may legitimately live upstream (pipeline behavior, caller).
    [Fact]
    public async Task IgnoresPublishWithoutSaveInMemberAsync()
    {
        var source = Preamble +
            """
            class C
            {
                Task<AxisResult> M(IAxisBus bus)
                    => bus.PublishAsync(new PingEvent());
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0300"));
    }

    // PublishAsync on a type that is not IAxisBus is someone else's method, never this diagnostic.
    [Fact]
    public async Task IgnoresUnrelatedPublishAsyncAfterSaveAsync()
    {
        var source = Preamble +
            """
            public interface ISchemaPublisher { Task<AxisResult> PublishAsync(string name); }
            class C
            {
                async Task<AxisResult> M(ISchemaPublisher publisher, IUow uow)
                {
                    await uow.SaveChangesAsync();
                    return await publisher.PublishAsync("v2");
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0300"));
    }

    // A later commit still drains the queue — publish between two saves is not the bug.
    [Fact]
    public async Task IgnoresPublishBetweenTwoSavesAsync()
    {
        var source = Preamble +
            """
            class C
            {
                async Task<AxisResult> M(IAxisBus bus, IUow uow)
                {
                    await uow.SaveChangesAsync();
                    await bus.PublishAsync(new PingEvent());
                    return await uow.SaveChangesAsync();
                }
            }
            """;

        var diagnostics = await AnalyzerHarness.RunAsync<PublishAfterSaveChangesAnalyzer>(source);

        Assert.Equal(0, diagnostics.Count("AXIS0300"));
    }
}
