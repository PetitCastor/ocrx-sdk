using Ocrx.Contracts.Proto;
using Xunit;
using ProtoFieldType = Ocrx.Contracts.Proto.SettingsFieldType;
using ProtoTrackRequest = Ocrx.Contracts.Proto.TrackRequest;
using ProtoTrackResponse = Ocrx.Contracts.Proto.TrackResponse;

namespace Ocrx.Sdk.Tests;

public class SettingsProtoMappingTests
{
    [Fact]
    public void SettingsSpec_RoundTripsEveryFieldShape()
    {
        var source = new SettingsSpec(
        [
            new SettingsField(
                "theme",
                "Theme",
                SettingsFieldType.Select,
                "dark",
                "Choose the display theme.",
                [new SettingsOption("dark", "Dark"), new SettingsOption("light", "Light")],
                Group: "Display"),
            new SettingsField("enabled", "Enabled", SettingsFieldType.Toggle, "true", Group: "Display"),
            new SettingsField("limit", "Limit", SettingsFieldType.Int, "3", Min: 0, Max: 10, Group: "Rules"),
            new SettingsField("opacity", "Opacity", SettingsFieldType.Float, "0.75", Min: 0, Max: 1, Group: "Rules"),
            new SettingsField("label", "Label", SettingsFieldType.String, "", Help: "", Group: ""),
        ]);

        var proto = source.ToProto();
        var roundTrip = proto.ToSettingsSpec();

        Assert.Equal(5, proto.Fields.Count);
        Assert.Equal(ProtoFieldType.Select, proto.Fields[0].Type);
        Assert.Equal(["dark", "light"], proto.Fields[0].Options.Select(o => o.Value));
        Assert.False(proto.Fields[0].HasMin);
        Assert.False(proto.Fields[0].HasMax);
        Assert.True(proto.Fields[2].HasMin);
        Assert.True(proto.Fields[2].HasMax);
        Assert.Equal(0, proto.Fields[2].Min);
        Assert.Equal(10, proto.Fields[2].Max);
        AssertSettingsSpec(source, roundTrip);
    }

    [Fact]
    public void SettingsSpec_EmptySpecRoundTrips()
    {
        var roundTrip = SettingsSpec.Empty.ToProto().ToSettingsSpec();

        Assert.Empty(roundTrip.Fields);
    }

    [Fact]
    public void ApplySettings_RoundTripsValueBatchVerbatim()
    {
        var source = new ApplySettings(
        [
            new SettingsValue("theme", "light"),
            new SettingsValue("notes", ""),
            new SettingsValue("opacity", "0.50"),
        ]);

        var roundTrip = source.ToProto().ToApplySettings();

        Assert.Equal(source.Values.Select(v => v.Id), roundTrip.Values.Select(v => v.Id));
        Assert.Equal(source.Values.Select(v => v.Value), roundTrip.Values.Select(v => v.Value));
    }

    [Fact]
    public void SettingsSpec_IsTheFieldThreeTrackRequestArm()
    {
        var request = new ProtoTrackRequest { Settings = SettingsSpec.Empty.ToProto() };

        Assert.Equal(3, ProtoTrackRequest.SettingsFieldNumber);
        Assert.Equal(ProtoTrackRequest.MsgOneofCase.Settings, request.MsgCase);
    }

    [Fact]
    public void ApplySettings_IsTheFieldThreeTrackResponseArm()
    {
        var response = new ProtoTrackResponse { ApplySettings = new ApplySettings([]).ToProto() };

        Assert.Equal(3, ProtoTrackResponse.ApplySettingsFieldNumber);
        Assert.Equal(ProtoTrackResponse.MsgOneofCase.ApplySettings, response.MsgCase);
    }

    [Fact]
    public void StreamEnd_IsTheFieldFourTrackResponseArm()
    {
        var response = new ProtoTrackResponse { StreamEnd = new Ocrx.Contracts.Proto.StreamEnd() };

        Assert.Equal(4, ProtoTrackResponse.StreamEndFieldNumber);
        Assert.Equal(ProtoTrackResponse.MsgOneofCase.StreamEnd, response.MsgCase);
    }

    [Fact]
    public void NewEngineWithOldPlugin_HasNoSpecToRender()
    {
        var request = new ProtoTrackRequest { Hello = new Hello { ClientName = "legacy-plugin" } };

        Assert.Equal(ProtoTrackRequest.MsgOneofCase.Hello, request.MsgCase);
        Assert.Null(request.Settings);
    }

    [Fact]
    public void UnknownSettingsFieldType_IsNotGuessed()
    {
        var proto = new Ocrx.Contracts.Proto.SettingsSpec();
        proto.Fields.Add(new Ocrx.Contracts.Proto.SettingsField
        {
            Id = "future",
            Label = "Future",
            Type = ProtoFieldType.Unspecified,
            Value = "x",
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => proto.ToSettingsSpec());
    }

    private static void AssertSettingsSpec(SettingsSpec expected, SettingsSpec actual)
    {
        Assert.Equal(expected.Fields.Count, actual.Fields.Count);
        for (var i = 0; i < expected.Fields.Count; i++)
        {
            var e = expected.Fields[i];
            var a = actual.Fields[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Label, a.Label);
            Assert.Equal(e.Help, a.Help);
            Assert.Equal(e.Type, a.Type);
            Assert.Equal(e.Value, a.Value);
            Assert.Equal(e.Min, a.Min);
            Assert.Equal(e.Max, a.Max);
            Assert.Equal(e.Group, a.Group);
            Assert.Equal(e.Options?.Select(o => (o.Value, o.Label)), a.Options?.Select(o => (o.Value, o.Label)));
        }
    }
}
