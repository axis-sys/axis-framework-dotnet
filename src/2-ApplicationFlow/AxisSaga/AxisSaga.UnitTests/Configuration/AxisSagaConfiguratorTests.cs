using Axis.Core;

namespace AxisSaga.UnitTests.Configuration;

public class AxisSagaConfiguratorTests
{
    public record TestPayload
    {
        public string SagaId { get; init; } = "";
        public string? Result { get; init; }
    }

    [Fact]
    public void Build_ShouldProduceDefinitionWithSagaNameAndPayloadType()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").FinishOnSuccess();

        var def = configurator.Build();

        Assert.Equal("TestSaga", def.SagaName);
        Assert.Equal(typeof(TestPayload), def.PayloadType);
        Assert.Single(def.ForwardStages);
        Assert.Empty(def.ErrorStages);
    }

    [Fact]
    public void AddStage_ShouldRegisterForwardStageInRegistrationOrder()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").NextStageOnSuccess("StageB");
        configurator.AddStage("StageB").FinishOnSuccess();

        var def = configurator.Build();

        Assert.Equal(2, def.ForwardStages.Count);
        Assert.Equal("StageA", def.ForwardStages[0].StageName);
        Assert.Equal("StageB", def.ForwardStages[1].StageName);
        Assert.False(def.ForwardStages[0].IsErrorStage);
        Assert.False(def.ForwardStages[1].IsErrorStage);
    }

    [Fact]
    public void AddErrorStage_ShouldRegisterAsErrorStage()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").FinishOnSuccess();
        configurator.AddErrorStage("Compensate");

        var def = configurator.Build();

        Assert.Single(def.ErrorStages);
        Assert.Equal("Compensate", def.ErrorStages[0].StageName);
        Assert.True(def.ErrorStages[0].IsErrorStage);
    }

    [Fact]
    public void OnSuccessNext_ShouldSetNextStageName()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").NextStageOnSuccess("StageB");
        configurator.AddStage("StageB").FinishOnSuccess();

        var def = configurator.Build();

        Assert.Equal("StageB", def.ForwardStages[0].NextStageOnSuccess);
        Assert.Null(def.ForwardStages[1].NextStageOnSuccess);
    }

    [Fact]
    public void OnErrorRouteTo_ShouldSetCompensationChain()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").RouteToOnError("Comp1", "Comp2").FinishOnSuccess();
        configurator.AddErrorStage("Comp1");
        configurator.AddErrorStage("Comp2");

        var def = configurator.Build();

        var stageA = def.ForwardStages[0];
        Assert.Equal(["Comp1", "Comp2"], stageA.RouteToOnError);
    }

    [Fact]
    public void Build_WithoutAnyForwardStage_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");

        Assert.Throws<InvalidOperationException>(configurator.Build);
    }

    [Fact]
    public void AddStage_WithDuplicateName_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").FinishOnSuccess();

        Assert.Throws<InvalidOperationException>(() => configurator.AddStage("StageA"));
    }

    [Fact]
    public void AddErrorStage_WithDuplicateNameOfForwardStage_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").FinishOnSuccess();

        Assert.Throws<InvalidOperationException>(() => configurator.AddErrorStage("StageA"));
    }

    [Fact]
    public void OnSuccessNext_ReferencingUnknownStage_ShouldFailAtBuild()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").NextStageOnSuccess("Missing");

        Assert.Throws<InvalidOperationException>(configurator.Build);
    }

    [Fact]
    public void OnErrorRouteTo_ReferencingUnknownStage_ShouldFailAtBuild()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").RouteToOnError("MissingComp").FinishOnSuccess();

        Assert.Throws<InvalidOperationException>(configurator.Build);
    }

    [Fact]
    public void OnSuccessNext_AfterOnSuccessFinish_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        var stage = configurator.AddStage("StageA").FinishOnSuccess();

        Assert.Throws<InvalidOperationException>(() => stage.NextStageOnSuccess("StageB"));
    }

    [Fact]
    public void OnSuccessFinish_AfterOnSuccessNext_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageB").FinishOnSuccess();
        var stage = configurator.AddStage("StageA").NextStageOnSuccess("StageB");

        Assert.Throws<InvalidOperationException>(stage.FinishOnSuccess);
    }

    [Fact]
    public void GetStage_ShouldFindForwardOrErrorStage()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").FinishOnSuccess();
        configurator.AddErrorStage("Comp1");

        var def = configurator.Build();

        Assert.NotNull(def.GetStage("StageA"));
        Assert.NotNull(def.GetStage("Comp1"));
        Assert.Null(def.GetStage("Unknown"));
    }

    [Fact]
    public void FirstForwardStage_ShouldReturnFirstRegistered()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("First").NextStageOnSuccess("Second");
        configurator.AddStage("Second").FinishOnSuccess();

        var def = configurator.Build();

        Assert.Equal("First", def.FirstForwardStage.StageName);
    }

    [Fact]
    public void AddStage_WithEmptyName_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");

        Assert.Throws<ArgumentException>(() => configurator.AddStage(""));
        Assert.Throws<ArgumentException>(() => configurator.AddStage("   "));
    }

    [Fact]
    public void RouteToOnError_WithNullArray_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        var stage = configurator.AddStage("StageA");

        Assert.Throws<ArgumentNullException>(() => stage.RouteToOnError(null!));
    }

    [Fact]
    public void RouteToOnError_WithEmptyName_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        var stage = configurator.AddStage("StageA");

        Assert.Throws<ArgumentException>(() => stage.RouteToOnError(""));
        Assert.Throws<ArgumentException>(() => stage.RouteToOnError("   "));
    }

    [Fact]
    public void NextStageOnSuccess_WithEmptyName_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        var stage = configurator.AddStage("StageA");

        Assert.Throws<ArgumentException>(() => stage.NextStageOnSuccess(""));
        Assert.Throws<ArgumentException>(() => stage.NextStageOnSuccess("   "));
    }

    [Fact]
    public void RetryOnTransient_ShouldRecordAttemptsAndDelayOnStage()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").RetryOnTransient(5, TimeSpan.FromMilliseconds(250)).FinishOnSuccess();

        var def = configurator.Build();

        Assert.Equal(5, def.ForwardStages[0].TransientRetryMaxAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(250), def.ForwardStages[0].TransientRetryBaseDelay);
    }

    [Fact]
    public void RetryOnTransient_WithoutBaseDelay_LeavesDelayNullToInheritGlobal()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").RetryOnTransient(2).FinishOnSuccess();

        var def = configurator.Build();

        Assert.Equal(2, def.ForwardStages[0].TransientRetryMaxAttempts);
        Assert.Null(def.ForwardStages[0].TransientRetryBaseDelay);
    }

    [Fact]
    public void RetryOnTransient_NotCalled_LeavesBothNullToInheritGlobal()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        configurator.AddStage("StageA").FinishOnSuccess();

        var def = configurator.Build();

        Assert.Null(def.ForwardStages[0].TransientRetryMaxAttempts);
        Assert.Null(def.ForwardStages[0].TransientRetryBaseDelay);
    }

    [Fact]
    public void RetryOnTransient_WithMaxAttemptsBelowOne_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        var stage = configurator.AddStage("StageA");

        Assert.Throws<ArgumentOutOfRangeException>(() => stage.RetryOnTransient(0));
    }

    [Fact]
    public void RetryOnTransient_WithNegativeDelay_ShouldThrow()
    {
        var configurator = new AxisSagaConfigurator<TestPayload>("TestSaga");
        var stage = configurator.AddStage("StageA");

        Assert.Throws<ArgumentOutOfRangeException>(() => stage.RetryOnTransient(3, TimeSpan.FromMilliseconds(-1)));
    }
}
