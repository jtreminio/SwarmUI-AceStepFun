using SwarmUI.Text2Image;
using SwarmUI.Utils;

namespace AceStepFun;

internal static class PromptParser
{
    public static string ResolvePrompt(T2IParamInput input)
    {
        return ResolvePrompt(input, 0);
    }

    public static string ResolvePrompt(T2IParamInput input, int trackIndex)
    {
        PromptRegion region = new(input.Get(T2IParamTypes.Prompt, ""));
        string trackPrompt = ExtractAudioPrompt(region, AceStepFunExtension.AudioSectionIdForTrack(trackIndex));
        if (!string.IsNullOrWhiteSpace(trackPrompt))
        {
            return trackPrompt.Trim();
        }

        string explicitPrompt = ResolveExplicitPrompt(input);
        if (explicitPrompt is not null)
        {
            return explicitPrompt;
        }

        string audioPrompt = ExtractAudioPrompt(region, AceStepFunExtension.SectionID_Audio);
        if (!string.IsNullOrWhiteSpace(audioPrompt))
        {
            return audioPrompt.Trim();
        }

        return region.GlobalPrompt.Trim();
    }

    public static bool HasAudioSection(string prompt)
    {
        PromptRegion region = new(prompt ?? "");
        foreach (PromptRegion.Part part in region.Parts)
        {
            if (part.Type == PromptRegion.PartType.CustomPart && IsAceStepPromptPrefix(part.Prefix))
            {
                return true;
            }
        }
        return false;
    }

    private static string ResolveExplicitPrompt(T2IParamInput input)
    {
        if (TryGetRawPrompt(input, AceStepFunExtension.Prompt, out string prompt)
            || TryGetRawPrompt(input, AceStepFunExtension.Text2AudioPrompt, out prompt))
        {
            return prompt.Trim();
        }
        return null;
    }

    private static bool TryGetRawPrompt(T2IParamInput input, T2IRegisteredParam<string> param, out string prompt)
    {
        prompt = "";
        if (!HasRaw(input, param))
        {
            return false;
        }
        prompt = input.Get(param, "");
        return !string.IsNullOrWhiteSpace(prompt);
    }

    private static string ExtractAudioPrompt(PromptRegion region, int contextId)
    {
        StringBuilder builder = new();
        foreach (PromptRegion.Part part in region.Parts)
        {
            if (part.Type == PromptRegion.PartType.CustomPart && IsMatchingPromptSection(part, contextId))
            {
                builder.Append(part.Prompt);
            }
        }
        return builder.ToString();
    }

    private static bool HasRaw<T>(T2IParamInput input, T2IRegisteredParam<T> param)
    {
        return param?.Type is not null && input.TryGetRaw(param.Type, out _);
    }

    private static bool IsAceStepPromptPrefix(string prefix)
    {
        return prefix == "acestepfun" || prefix == "audio";
    }

    private static bool IsMatchingPromptSection(PromptRegion.Part part, int contextId)
    {
        if (part.Prefix == "acestepfun")
        {
            return part.ContextID == contextId;
        }
        return part.Prefix == "audio"
            && contextId == AceStepFunExtension.SectionID_Audio
            && part.ContextID == AceStepFunExtension.SectionID_Audio;
    }
}
