namespace WinTendedero.UI;

public static partial class Strings
{
    private static string ActivationText(string en, string zh, string ar) => Language switch { "zh" => zh, "ar" => ar, _ => en };
    public static string SectionActivation => ActivationText("Activation", "呼出方式", "التفعيل");
    public static string RevealDelayLabel => ActivationText("Reveal delay", "唤出等待", "مهلة الإظهار");
    public static string RevealDelayHelp => ActivationText("Shorter is faster. Zero reveals immediately.", "越短越灵敏；设为 0 毫秒时立即唤出。", "كلما قصرت المهلة زادت السرعة. الصفر يعني الإظهار فورًا.");
    public static string RevealBandLabel => ActivationText("Top activation area", "顶部触发范围", "نطاق التفعيل العلوي");
    public static string RevealBandHelp => ActivationText("A wider area is easier to reach. Moving along the top still counts.", "范围越大越容易唤出，沿顶部移动也能触发。", "النطاق الأوسع أسهل وصولًا. الحركة على الحافة لا تعيد الانتظار.");
    public static string EdgeActivationHelp => ActivationText("Move into the top area and wait for the configured delay. Clicks and drags do not reveal the line.", "鼠标进入顶部区域后按设定时间唤出；点击和拖拽时不会误弹出。", "ضع المؤشر في النطاق العلوي وانتظر المهلة المحددة. النقر والسحب لا يظهران الحبل.");
    public static string DelayValue(double milliseconds) => ActivationText($"{milliseconds:0} ms", $"{milliseconds:0} 毫秒", $"{milliseconds:0} مللي ثانية");
    public static string BandValue(double pixels) => ActivationText($"{pixels:0} px", $"{pixels:0} 像素", $"{pixels:0} بكسل");
    public static string SectionShortcut => ActivationText("Keyboard shortcut", "快捷键", "اختصار لوحة المفاتيح");
    public static string ChangeShortcut => ActivationText("Change", "更改", "تغيير");
    public static string CancelShortcut => ActivationText("Cancel", "取消", "إلغاء");
    public static string PressShortcut => ActivationText("Press a key combination…", "请按下组合键…", "اضغط مجموعة المفاتيح…");
    public static string ShortcutRecording => ActivationText("Press your new shortcut. Esc cancels.", "按下想使用的组合键，Esc 取消。", "اضغط الاختصار الجديد. Esc للإلغاء.");
    public static string ShortcutReady => ActivationText("Active. Choose Change to record a different shortcut.", "已生效，点“更改”可重新录入。", "الاختصار مفعل. اختر تغيير لتسجيل اختصار آخر.");
    public static string ShortcutUnavailable => ActivationText("This shortcut is unavailable. Choose Change to set another one.", "当前快捷键被占用，请点“更改”设置新的组合键。", "هذا الاختصار غير متاح. اختر تغيير لتعيين اختصار آخر.");
    public static string ShortcutConflict(string text) => ActivationText($"{text} is unavailable. Your previous shortcut is unchanged.", $"{text} 无法使用或已被占用，原快捷键保持不变。", $"الاختصار {text} غير متاح. لم يتغير الاختصار السابق.");
    public static string ShortcutInvalid => ActivationText("Use Ctrl, Alt or Win with a key, or a function key.", "请使用 Ctrl、Alt 或 Win 加一个按键，或使用功能键。", "استخدم Ctrl أو Alt أو Win مع مفتاح، أو مفتاح وظيفة.");
    public static string ShortcutRecorderUnavailable => ActivationText("Could not start recording. Reopen Settings and try again.", "暂时无法开始录入，请重新打开设置后再试。", "تعذر بدء التسجيل. أعد فتح الإعدادات وحاول مجددًا.");
}
