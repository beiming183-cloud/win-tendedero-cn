namespace WinTendedero.UI;

public static partial class Strings
{
    public static string StylePresets => ActivationText("Style presets","一键搭配","أنماط جاهزة");
    public static string MaterialLabel => ActivationText("Card material","卡片材质","خامة البطاقة");
    public static string AccentLabel => ActivationText("Frame colour","相框颜色","لون الإطار");
    public static string CustomColor => ActivationText("Choose colour…","自选颜色…","اختيار اللون…");
    public static string CordLabel => ActivationText("Line material","绳子材质","خامة الحبل");
    public static string ElasticityLabel => ActivationText("Bounce","Q 弹程度","مرونة الحركة");
    public static string ElasticityHint => ActivationText("Calm at zero; playful at maximum. A held photo tracks the pointer immediately.","0 更稳，100 更活泼；拖动始终跟手，松开后再回弹。","الصفر هادئ والحد الأعلى أكثر مرونة. الصورة تتبع المؤشر فورًا.");
    public static string ArrivalLabel=>ActivationText("Capture arrival","截图入场","ظهور اللقطة");
    public static string ArrivalBounce=>ActivationText("Light bounce","轻盈回弹","ارتداد خفيف");
    public static string ArrivalFlight=>ActivationText("Fly from capture position","从截图位置飞入","الطيران من موضع اللقطة");
    public static string MaterialName(string key) => key switch
    {
        "paper"=>ActivationText("Warm paper","暖纸相框","ورق دافئ"),
        "metal"=>ActivationText("Brushed metal","磨砂金属","معدن مصقول"),
        "soft"=>ActivationText("Soft candy","奶油软糖","حلوى ناعمة"),
        _=>ActivationText("Clear glass","清透玻璃","زجاج صافٍ"),
    };
    public static string CordName(string key) => key switch
    {
        "cotton"=>ActivationText("Cotton cord","编织棉绳","حبل قطني"),
        "wire"=>ActivationText("Metal wire","金属细线","سلك معدني"),
        _=>ActivationText("Fine thread","轻盈细绳","خيط رفيع"),
    };
    public static string PresetName(string key) => key switch
    {
        "warm"=>ActivationText("Warm wood","暖纸原木","خشب دافئ"),
        "steel"=>ActivationText("Cool steel","冷调金属","معدن بارد"),
        "candy"=>ActivationText("Peach candy","蜜桃软糖","حلوى الخوخ"),
        "mint"=>ActivationText("Mint cream","薄荷奶油","كريمة النعناع"),
        _=>ActivationText("Clear sky","晴空玻璃","سماء صافية"),
    };
}
