using Newtonsoft.Json.Linq;
using SwarmUI.Builtin_ComfyUIBackend;
using SwarmUI.Text2Image;

namespace AceStepFun;

public class JsonParser(WorkflowGenerator g)
{
    private const string DefaultLmModel = "AceStep/qwen_1.7b_ace15.safetensors";

    public sealed record TrackSpec(
        int Index,
        string Prompt,
        string Style,
        double Duration,
        long Bpm,
        string TimeSignature,
        string Language,
        string KeyScale,
        string LmModel,
        double LmCfgScale,
        double AudioCfg,
        int Steps,
        double SigmaShift,
        string AudioSamplerName,
        string AudioScheduler
    );

    public List<TrackSpec> ParseTracks()
    {
        List<JObject> rawTracks = [BuildRootTrackObject()];
        rawTracks.AddRange(GetJsonTracksArray());

        List<TrackSpec> tracks = [];
        TrackSpec previousTrack = null;
        for (int i = 0; i < rawTracks.Count; i++)
        {
            TrackSpec inheritedTrack = ParseTrack(rawTracks[i], i, previousTrack);
            if (i == 0)
            {
                inheritedTrack = ApplyGlobalTrackOverrides(inheritedTrack);
            }

            TrackSpec track = ApplyTrackSpecificOverrides(inheritedTrack);
            tracks.Add(track);
            previousTrack = inheritedTrack;
        }
        return tracks;
    }

    private JObject BuildRootTrackObject()
    {
        return new JObject
        {
            ["Style"] = GetUserParam(
                AceStepFunExtension.Style,
                T2IParamTypes.Text2AudioStyle
            ),
            ["Duration"] = GetUserParam(
                AceStepFunExtension.Duration,
                T2IParamTypes.Text2AudioDuration
            ),
            ["Bpm"] = GetUserParam(
                AceStepFunExtension.Bpm,
                T2IParamTypes.Text2AudioBPM
            ),
            ["TimeSignature"] = GetUserParam(
                AceStepFunExtension.TimeSignature,
                T2IParamTypes.Text2AudioTimeSignature
            ),
            ["Language"] = GetUserParam(
                AceStepFunExtension.Language,
                T2IParamTypes.Text2AudioLanguage
            ),
            ["KeyScale"] = GetUserParam(
                AceStepFunExtension.KeyScale,
                T2IParamTypes.Text2AudioKeyScale
            ),
            ["LmModel"] = GetUserParam(
                AceStepFunExtension.LmModel,
                AceStepFunExtension.Text2AudioLmModel
            ),
            ["LmCfgScale"] = GetUserParam(
                AceStepFunExtension.LmCfg,
                AceStepFunExtension.Text2AudioLmCfg
            ),
            ["AudioCfg"] = GetUserParam(
                AceStepFunExtension.AudioCfg,
                AceStepFunExtension.Text2AudioAudioCfg
            ),
            ["Steps"] = GetUserParam(
                AceStepFunExtension.Steps,
                AceStepFunExtension.Text2AudioSteps
            ),
            ["SigmaShift"] = g.UserInput.Get(AceStepFunExtension.Text2AudioSigmaShift, 3.0),
            ["AudioSamplerName"] = "euler",
            ["AudioScheduler"] = "simple"
        };
    }

    private List<JObject> GetJsonTracksArray()
    {
        if (!g.UserInput.TryGet(AceStepFunExtension.MusicTracks, out string json)
            || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            JToken token = JToken.Parse(json);
            if (token is not JArray arr)
            {
                return [];
            }

            return [.. arr.OfType<JObject>()];
        }
        catch
        {
            return [];
        }
    }

    private TrackSpec ParseTrack(JObject trackObj, int index, TrackSpec previousTrack)
    {
        string fallbackStyle = previousTrack?.Style ?? "";
        double fallbackDuration = previousTrack?.Duration ?? 120.0;
        long fallbackBpm = previousTrack?.Bpm ?? 120L;
        string fallbackTimeSignature = previousTrack?.TimeSignature ?? "4";
        string fallbackLanguage = previousTrack?.Language ?? "en";
        string fallbackKeyScale = previousTrack?.KeyScale ?? "E minor";
        string fallbackLmModel = previousTrack?.LmModel ?? DefaultLmModel;
        double fallbackLmCfgScale = previousTrack?.LmCfgScale ?? 2.0;
        double fallbackAudioCfg = previousTrack?.AudioCfg ?? 2.0;
        int fallbackSteps = previousTrack?.Steps ?? 8;
        double fallbackSigmaShift = previousTrack?.SigmaShift ?? 3.0;
        string fallbackAudioSamplerName = previousTrack?.AudioSamplerName ?? "euler";
        string fallbackAudioScheduler = previousTrack?.AudioScheduler ?? "simple";

        return new(
            Index: index,
            Prompt: ResolvePrompt(index),
            Style: PickStringOrDefault(GetStr("Style", trackObj), fallbackStyle),
            Duration: GetDouble("Duration", trackObj) ?? fallbackDuration,
            Bpm: GetLong("Bpm", trackObj) ?? fallbackBpm,
            TimeSignature: PickStringOrDefault(GetStr("TimeSignature", trackObj), fallbackTimeSignature),
            Language: PickStringOrDefault(GetStr("Language", trackObj), fallbackLanguage),
            KeyScale: PickStringOrDefault(GetStr("KeyScale", trackObj), fallbackKeyScale),
            LmModel: PickStringOrDefault(GetStr("LmModel", trackObj), fallbackLmModel),
            LmCfgScale: GetDouble("LmCfgScale", trackObj) ?? fallbackLmCfgScale,
            AudioCfg: GetDouble("AudioCfg", trackObj) ?? fallbackAudioCfg,
            Steps: (int)(GetLong("Steps", trackObj) ?? fallbackSteps),
            SigmaShift: GetDouble("SigmaShift", trackObj) ?? fallbackSigmaShift,
            AudioSamplerName: PickStringOrDefault(GetStr("AudioSamplerName", trackObj), fallbackAudioSamplerName),
            AudioScheduler: PickStringOrDefault(GetStr("AudioScheduler", trackObj), fallbackAudioScheduler)
        );
    }

    private TrackSpec ApplyGlobalTrackOverrides(TrackSpec track)
    {
        return ApplySectionOverrides(track, AceStepFunExtension.SectionID_Audio);
    }

    private TrackSpec ApplyTrackSpecificOverrides(TrackSpec track)
    {
        return ApplySectionOverrides(track, AceStepFunExtension.AceStepSectionIdForTrack(track.Index));
    }

    private TrackSpec ApplySectionOverrides(TrackSpec track, int sectionId)
    {
        return track with
        {
            Style = GetSectionParamOrDefault(AceStepFunExtension.Style, T2IParamTypes.Text2AudioStyle, sectionId, track.Style),
            Duration = GetSectionParamOrDefault(AceStepFunExtension.Duration, T2IParamTypes.Text2AudioDuration, sectionId, track.Duration),
            Bpm = GetSectionParamOrDefault(AceStepFunExtension.Bpm, T2IParamTypes.Text2AudioBPM, sectionId, track.Bpm),
            TimeSignature = GetSectionParamOrDefault(AceStepFunExtension.TimeSignature, T2IParamTypes.Text2AudioTimeSignature, sectionId, track.TimeSignature),
            Language = GetSectionParamOrDefault(AceStepFunExtension.Language, T2IParamTypes.Text2AudioLanguage, sectionId, track.Language),
            KeyScale = GetSectionParamOrDefault(AceStepFunExtension.KeyScale, T2IParamTypes.Text2AudioKeyScale, sectionId, track.KeyScale),
            LmModel = GetSectionParamOrDefault(AceStepFunExtension.LmModel, AceStepFunExtension.Text2AudioLmModel, sectionId, track.LmModel),
            LmCfgScale = GetSectionParamOrDefault(AceStepFunExtension.LmCfg, AceStepFunExtension.Text2AudioLmCfg, sectionId, track.LmCfgScale),
            AudioCfg = GetSectionParamOrDefault(AceStepFunExtension.AudioCfg, AceStepFunExtension.Text2AudioAudioCfg, sectionId, track.AudioCfg),
            Steps = (int)GetSectionParamOrDefault(AceStepFunExtension.Steps, AceStepFunExtension.Text2AudioSteps, sectionId, (long)track.Steps),
            SigmaShift = GetSectionParamOrDefault(AceStepFunExtension.Text2AudioSigmaShift, sectionId, track.SigmaShift),
            AudioSamplerName = GetSectionParamOrDefault(ComfyUIBackendExtension.SamplerParam, sectionId, track.AudioSamplerName),
            AudioScheduler = GetSectionParamOrDefault(ComfyUIBackendExtension.SchedulerParam, sectionId, track.AudioScheduler)
        };
    }

    private string ResolvePrompt(int trackIndex)
    {
        return PromptParser.ResolvePrompt(g.UserInput, trackIndex);
    }

    private static string GetStr(string key, JObject obj)
    {
        foreach (JProperty p in obj.Properties())
        {
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                return p.Value?.Type == JTokenType.Null ? null : $"{p.Value}";
            }
        }
        return null;
    }

    private static double? GetDouble(string key, JObject obj)
    {
        string value = GetStr(key, obj);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (double.TryParse(value, out double parsed))
        {
            return parsed;
        }
        return null;
    }

    private static long? GetLong(string key, JObject obj)
    {
        string value = GetStr(key, obj);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (long.TryParse(value, out long parsed))
        {
            return parsed;
        }
        return null;
    }

    private static string PickStringOrDefault(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private T GetUserParam<T>(T2IRegisteredParam<T> primary, T2IRegisteredParam<T> fallback)
    {
        T defaultValue = GetParamDefault(primary);
        bool hasPrimary = HasRaw(primary);
        bool hasFallback = HasRaw(fallback);
        T primaryValue = hasPrimary ? g.UserInput.Get(primary, defaultValue) : defaultValue;
        T fallbackValue = hasFallback ? g.UserInput.Get(fallback, defaultValue) : defaultValue;

        if (hasPrimary && !hasFallback)
        {
            return primaryValue;
        }
        if (!hasPrimary && hasFallback)
        {
            return fallbackValue;
        }
        if (hasPrimary && hasFallback)
        {
            if (EqualityComparer<T>.Default.Equals(primaryValue, defaultValue))
            {
                return fallbackValue;
            }
            return primaryValue;
        }
        return g.UserInput.Get(primary, defaultValue);
    }

    private T GetSectionParamOrDefault<T>(
        T2IRegisteredParam<T> primary,
        T2IRegisteredParam<T> fallback,
        int sectionId,
        T currentValue)
    {
        if (TryGetSectionParam(primary, sectionId, out T value)
            || TryGetSectionParam(fallback, sectionId, out value))
        {
            return value;
        }
        return currentValue;
    }

    private T GetSectionParamOrDefault<T>(T2IRegisteredParam<T> param, int sectionId, T currentValue)
    {
        return TryGetSectionParam(param, sectionId, out T value) ? value : currentValue;
    }

    private bool TryGetSectionParam<T>(T2IRegisteredParam<T> param, int sectionId, out T value)
    {
        value = default;
        return param?.Type is not null && g.UserInput.TryGet(param, out value, sectionId, includeBase: false);
    }

    private static T GetParamDefault<T>(T2IRegisteredParam<T> param)
    {
        if (param?.Type is null)
        {
            return default;
        }

        T2IParamSet defaultSet = new();
        defaultSet.Set(param.Type, param.Type.Default ?? "");
        return defaultSet.Get(param);
    }

    private bool HasRaw<T>(T2IRegisteredParam<T> param)
    {
        return param?.Type is not null && g.UserInput.TryGetRaw(param.Type, out _);
    }
}
