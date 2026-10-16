namespace WinTendedero.UI;

public static partial class Strings
{
    public static string FrameLabel=>ActivationText("Photo frame","相框样式","نمط إطار الصورة");
    public static string FrameHint=>ActivationText("Adjust the rim, mat and corners. Changing style restores its recommended dimensions.","边框、留白和圆角自由调；切换样式会采用该样式的推荐尺寸。","اضبط الحافة والهامش والزوايا. تغيير النمط يعيد أبعاده الموصى بها.");
    public static string FrameWidthLabel=>ActivationText("Rim width","边框粗细","عرض الحافة");
    public static string FramePaddingLabel=>ActivationText("Photo mat","照片留白","هامش الصورة");
    public static string FrameRadiusLabel=>ActivationText("Corner radius","圆角大小","استدارة الزوايا");
    public static string ResetFrame=>ActivationText("Reset frame dimensions","恢复相框推荐尺寸","استعادة أبعاد الإطار");
    public static string FrameName(string key)=>key switch
    {
        "thin"=>ActivationText("Fine rim","极简细框","حافة رفيعة"),
        "polaroid"=>ActivationText("Instant photo","拍立得留白","صورة فورية"),
        "double"=>ActivationText("Gallery double rim","画廊双层框","إطار مزدوج"),
        "wood"=>ActivationText("Wooden frame","原木相框","إطار خشبي"),
        _=>ActivationText("Classic compact","轻盈小框","إطار مدمج"),
    };
}
