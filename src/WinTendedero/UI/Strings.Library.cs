namespace WinTendedero.UI;

public static partial class Strings
{
    public static string LibraryOpen => ActivationText("Open all screenshots", "打开全部截图", "فتح كل لقطات الشاشة");
    public static string PreviousPage => ActivationText("Previous screenshots", "上一页截图", "الصفحة السابقة");
    public static string NextPage => ActivationText("Next screenshots", "下一页截图", "الصفحة التالية");
    public static string CaptureSize => ActivationText("Card size", "截图卡片大小", "حجم الصور");
    public static string CaptureSizeHint => ActivationText("Larger cards make small details easier to read.", "调大后更容易看清细节，超出一屏可翻页。", "كبّر الصور لقراءة التفاصيل.");
    public static string CaptureLimit => ActivationText("Screenshots on the rope", "绳上最多保留", "عدد الصور على الحبل");
    public static string CaptureLimitHint => ActivationText("Older captures stay in the folder. Scroll over the rope or use the arrows to turn pages.", "超过上限的截图仍保存在文件夹；在绳上滚轮或点箭头翻页。", "تبقى الصور القديمة محفوظة. استخدم الأسهم لتصفح الصفحات.");
    public static string CaptureFolderHint => ActivationText("Restarting clears the rope. All screenshots stay in this folder. Changing the folder copies existing images; originals are kept.", "重启后绳子清空，截图持续保存在此。更改位置时会复制已有截图，原文件保留。", "عند إعادة التشغيل يفرغ الحبل وتبقى الصور محفوظة. تغيير المجلد ينسخ الصور مع الاحتفاظ بالأصل.");
    public static string FolderChangeFailed => ActivationText("Could not copy screenshots. Check permissions and free space; the previous folder is still in use.", "未能复制截图，请检查文件夹权限和磁盘空间，当前保存位置未更改。", "تعذر نسخ الصور. تحقق من الأذونات والمساحة.");
}
