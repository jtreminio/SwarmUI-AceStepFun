using System.Text;
using SwarmUI.Text2Image;
using SwarmUI.Utils;

namespace AceStepFun;

internal static class PromptParser
{
    public static string ResolvePrompt(T2IParamInput input)
    {
        string explicitPrompt = ResolveExplicitPrompt(input);
        if (explicitPrompt is not null)
        {
            return explicitPrompt.Trim();
        }

        PromptRegion region = new(input.Get(T2IParamTypes.Prompt, ""));
        string audioPrompt = ExtractAudioPrompt(region);
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
            if (part.Type == PromptRegion.PartType.CustomPart && part.Prefix == "audio")
            {
                return true;
            }
        }
        return false;
    }

    private static string ResolveExplicitPrompt(T2IParamInput input)
    {
        if (HasRaw(input, AceStepFunExtension.Prompt))
        {
            string prompt = input.Get(AceStepFunExtension.Prompt, "");
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return prompt;
            }
        }
        if (HasRaw(input, AceStepFunExtension.Text2AudioPrompt))
        {
            string prompt = input.Get(AceStepFunExtension.Text2AudioPrompt, "");
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                return prompt;
            }
        }
        return null;
    }

    private static string ExtractAudioPrompt(PromptRegion region)
    {
        StringBuilder builder = new();
        foreach (PromptRegion.Part part in region.Parts)
        {
            if (part.Type == PromptRegion.PartType.CustomPart && part.Prefix == "audio")
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
}
