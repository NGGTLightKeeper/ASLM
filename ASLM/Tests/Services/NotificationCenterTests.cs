// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Services;

namespace ASLM.Tests.Services;

public sealed class NotificationCenterTests
{
    [Theory]
    [InlineData("Module", "ASLM-Chat", "module:aslm-chat")]
    [InlineData(" Engine ", " Ollama ", "engine:ollama")]
    public void BuildOperationKey_normalizes_source_parts(string kind, string id, string expected)
    {
        NotificationCenter.BuildOperationKey(kind, id).Should().Be(expected);
    }
}
