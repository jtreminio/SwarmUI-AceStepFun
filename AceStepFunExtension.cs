using SwarmUI.Core;
using SwarmUI.Text2Image;
using SwarmUI.Utils;
using SwarmUI.Builtin_ComfyUIBackend;

namespace AceStepFun;

public class AceStepFunExtension : Extension
{
    public const int SectionID_Audio = 55425;
    private const string ComfyUIFeatureFlag = "comfyui";

    public static T2IRegisteredParam<T2IModel> Model;
    public static T2IRegisteredParam<string> Prompt;
    public static T2IRegisteredParam<string> Style;
    public static T2IRegisteredParam<double> AudioCfg;
    public static T2IRegisteredParam<double> LmCfg;
    public static T2IRegisteredParam<long> Steps;
    public static T2IRegisteredParam<string> LmModel;
    public static T2IRegisteredParam<double> Duration;
    public static T2IRegisteredParam<long> Bpm;
    public static T2IRegisteredParam<string> TimeSignature;
    public static T2IRegisteredParam<string> Language;
    public static T2IRegisteredParam<string> KeyScale;
    public static T2IRegisteredParam<string> Text2AudioPrompt;
    public static T2IRegisteredParam<string> Text2AudioLmModel;
    public static T2IRegisteredParam<double> Text2AudioAudioCfg;
    public static T2IRegisteredParam<double> Text2AudioLmCfg;
    public static T2IRegisteredParam<long> Text2AudioSteps;
    public static T2IRegisteredParam<double> Text2AudioSigmaShift;
    private static readonly List<string> LmModelOptions =
    [
        "AceStep/qwen_0.6b_ace15.safetensors",
        "AceStep/qwen_1.7b_ace15.safetensors",
        "AceStep/qwen_4b_ace15.safetensors"
    ];

    public override void OnPreInit()
    {
        ScriptFiles.Add("Assets/acestepfun.js");
        PromptRegion.RegisterCustomPrefix("audio");

        T2IPromptHandling.PromptTagBasicProcessors["audio"] = (data, context) =>
        {
            context.SectionID = SectionID_Audio;
            return $"<audio//cid={SectionID_Audio}>";
        };
        T2IPromptHandling.PromptTagLengthEstimators["audio"] = (data, context) => "<break>";
    }

    public override void OnInit()
    {
        Logs.Info("AceStepFun Extension initializing...");
        RegisterParameters();
        WorkflowGenerator.AddStep(generator => new Runner(generator).Run(), 10);
    }

    private static void RegisterParameters()
    {
        T2IParamGroup AceStepFunGroup = new(
            Name: "AceStepFun",
            Description: "Generate AceStep audio workflows.",
            Toggles: true,
            Open: false,
            OrderPriority: -2.7
        );

        double OrderPriority = 0;

        Model = T2IParamTypes.Register<T2IModel>(new T2IParamType(
            Name: "AceStepFun Model",
            Description: "Model to use for audio generation. Ignored when Audio model selected in GUI models section.",
            Default: "",
            GetValues: s => T2IParamTypes.CleanModelList(
                Program.MainSDModels.ListModelsFor(s)
                    .Where(m => m.ModelClass?.ID == "ace-step-1_5")
                    .Select(m => m.Name)
            ),
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            Subtype: "Stable-Diffusion",
            ChangeWeight: 9,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        LmModel = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "AceStepFun LM Model",
            Description: "LM Model.",
            Default: "AceStep/qwen_1.7b_ace15.safetensors",
            GetValues: _ => LmModelOptions,
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        Duration = T2IParamTypes.Register<double>(new T2IParamType(
            Name: "AceStepFun Duration",
            Description: "Audio duration in seconds.",
            Default: "120",
            Min: 1,
            Max: 600,
            Step: 1,
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        Style = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "AceStepFun Style",
            Description: "Style or genre tags.",
            Default: "",
            ViewType: ParamViewType.PROMPT,
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        Bpm = T2IParamTypes.Register<long>(new T2IParamType(
            Name: "AceStepFun BPM",
            Description: "Beats per minute.",
            Default: "120",
            Min: 40,
            Max: 300,
            Step: 1,
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        TimeSignature = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "AceStepFun Time Signature",
            Description: "Time signature.",
            Default: "4",
            GetValues: s => T2IParamTypes.Text2AudioTimeSignature.Type.GetValues?.Invoke(s),
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        Language = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "AceStepFun Language",
            Description: "Language for the prompt.",
            Default: "en",
            GetValues: s => T2IParamTypes.Text2AudioLanguage.Type.GetValues?.Invoke(s),
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        KeyScale = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "AceStepFun Key Scale",
            Description: "Key and scale for the music.",
            Default: "E minor",
            GetValues: s => T2IParamTypes.Text2AudioKeyScale.Type.GetValues?.Invoke(s),
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        AudioCfg = T2IParamTypes.Register<double>(new T2IParamType(
            Name: "AceStepFun Audio CFG",
            Description: "Audio CFG.",
            Default: "1",
            Min: 1,
            Max: 10,
            Step: 0.5,
            ViewType: ParamViewType.SLIDER,
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        LmCfg = T2IParamTypes.Register<double>(new T2IParamType(
            Name: "AceStepFun LM CFG",
            Description: "LM CFG.",
            Default: "2",
            Min: 1,
            Max: 10,
            Step: 0.5,
            Group: AceStepFunGroup,
            FeatureFlag: ComfyUIFeatureFlag,
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));
        T2IParamTypes.ParameterRemaps[T2IParamTypes.CleanTypeName("AceStepFun Sampler CFG")] = LmCfg.Type.ID;
        OrderPriority += 1;

        Steps = T2IParamTypes.Register<long>(new T2IParamType(
            Name: "AceStepFun Steps",
            Description: "Sampling steps.",
            Default: "8",
            Min: 1,
            Max: 100,
            Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: AceStepFunGroup,
            OrderPriority: OrderPriority,
            FeatureFlag: ComfyUIFeatureFlag,
            DoNotPreview: true
        ));
        OrderPriority += 1;

        Prompt = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "AceStepFun Prompt",
            Description: "",
            Default: "",
            Group: AceStepFunGroup,
            FeatureFlag: ComfyUIFeatureFlag,
            ViewType: ParamViewType.BIG,
            VisibleNormally: false,
            DoNotPreview: true
        ));

        Text2AudioPrompt = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Text2Audio Prompt",
            Description: "Prompt for text2audio generation.",
            Default: "",
            Group: AceStepFunGroup,
            FeatureFlag: "text2audio",
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));

        Text2AudioLmModel = T2IParamTypes.Register<string>(new T2IParamType(
            Name: "Text2Audio LM Model",
            Description: "LM Model for text2audio generation.",
            Default: "AceStep/qwen_1.7b_ace15.safetensors",
            GetValues: _ => LmModelOptions,
            Group: AceStepFunGroup,
            FeatureFlag: "text2audio",
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));

        Text2AudioAudioCfg = T2IParamTypes.Register<double>(new T2IParamType(
            Name: "Text2Audio Audio CFG",
            Description: "Audio sampler CFG for text2audio generation.",
            Default: "2",
            Min: 1,
            Max: 10,
            Step: 0.5,
            ViewType: ParamViewType.SLIDER,
            Group: AceStepFunGroup,
            FeatureFlag: "text2audio",
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));

        Text2AudioLmCfg = T2IParamTypes.Register<double>(new T2IParamType(
            Name: "Text2Audio LM CFG",
            Description: "LM CFG for text2audio generation.",
            Default: "2",
            Min: 1,
            Max: 10,
            Step: 0.5,
            Group: AceStepFunGroup,
            FeatureFlag: "text2audio",
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));
        T2IParamTypes.ParameterRemaps[T2IParamTypes.CleanTypeName("Text2Audio Sampler CFG")] = Text2AudioLmCfg.Type.ID;

        Text2AudioSteps = T2IParamTypes.Register<long>(new T2IParamType(
            Name: "Text2Audio Steps",
            Description: "Sampling steps for text2audio generation.",
            Default: "8",
            Min: 1,
            Max: 100,
            Step: 1,
            ViewType: ParamViewType.SLIDER,
            Group: AceStepFunGroup,
            FeatureFlag: "text2audio",
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));

        Text2AudioSigmaShift = T2IParamTypes.Register<double>(new T2IParamType(
            Name: "Text2Audio Sigma Shift",
            Description: "",
            Default: "3",
            Min: 1,
            Max: 100,
            Group: AceStepFunGroup,
            FeatureFlag: "text2audio",
            VisibleNormally: false,
            ExtraHidden: true,
            DoNotPreview: true
        ));
    }
}
