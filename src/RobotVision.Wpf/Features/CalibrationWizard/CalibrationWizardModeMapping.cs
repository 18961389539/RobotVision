using RobotVision.Hosting;

namespace RobotVision.WpfHost.Features.CalibrationWizard;

/// <summary>UI <see cref="WizardMode"/> 与 Hosting <see cref="CalibrationWizardMode"/> 的显式映射（禁止裸强转）。</summary>
internal static class CalibrationWizardModeMapping
{
    public static CalibrationWizardMode ToHosting(WizardMode mode) => mode switch
    {
        WizardMode.Intrinsic => CalibrationWizardMode.Intrinsic,
        WizardMode.Extrinsic => CalibrationWizardMode.Extrinsic,
        WizardMode.Rotation => CalibrationWizardMode.Rotation,
        WizardMode.Polynomial => CalibrationWizardMode.Polynomial,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知标定模式"),
    };

    public static WizardMode ToWizard(CalibrationWizardMode mode) => mode switch
    {
        CalibrationWizardMode.Intrinsic => WizardMode.Intrinsic,
        CalibrationWizardMode.Extrinsic => WizardMode.Extrinsic,
        CalibrationWizardMode.Rotation => WizardMode.Rotation,
        CalibrationWizardMode.Polynomial => WizardMode.Polynomial,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知标定模式"),
    };
}
