# PROJECT MEMORY — LATCHI TELEPROMPTER

ذاكرة المشروع للصيانة المستقبلية. تُحدَّث مع كل إصدار.

## القرارات المعمارية (ولماذا)

| القرار | السبب |
|---|---|
| WPF خالص، بلا أطر UI خارجية | مواصفة §? (1111.txt): جهاز ضعيف 4GB/HDD/بلا GPU — WPF أفتح خيار .NET لسطح المكتب |
| فصل Core (net8.0) عن App (net8.0-windows) | اختبار منطق التمرير/المكتبة/الحفظ على أي منصة، وApp مجرد قشرة عرض |
| التمرير عبر `DoubleAnimation` على `TranslateTransform` (ScrollAnimator) | يعمل على render thread: ناعم على GPU ضعيف، وصفر CPU عند الإيقاف (لا timer) |
| `TeleprompterEngine` آلة حالة نقية (offset/MaxOffset/speed) | كل قواعد التقدم قابلة للاختبار؛ WPF يقرأ الحالة فقط |
| SpeedCurve `PxPerSecond = 4+3v` (7..304 px/s) + `WPM = 60+1.5v` | منحنى خطي يُحسّ به المستخدم طبيعياً عبر كامل المدى 1..100 |
| كتابة ذرية (tmp + File.Replace) لكل ملف بيانات | أولوية «منع فقدان البيانات» فوق كل شيء (§70) — انقطاع كهرباء لا يفسد ملفاً |
| AutosaveService: Timer 900ms debounce + قفل + ForceWrite عند Dispose | كتابة واحدة بعد توقف الكتابة، لا سباق خيوط، لا خسارة عند الإغلاق |
| ملف قصة تالف يُتجاهل (لا يمنع فتح المكتبة) | عزل الأعطال: ملف واحد لا يكسر التطبيق |
| settings.json تالف → افتراضيات | نفس مبدأ العزل |
| علامات `[توقف Xث]` نص مرئي في V1 | أبسط UX ممكن؛ ScriptParser بنيته جاهزة للترقية إلى إيقاف فعلي لاحقاً |
| Dark title bar عبر `DwmSetWindowAttribute(20)` | يجعل شريط عنوان النافذة داكناً مثل الواجهة على Win10/11 |
| نشر **self-contained مجلد** (ليس single-file) | HDD بطيء + استخراج single-file عند كل تشغيل يهزم الهدف؛ R2R يسرّع الإقلاع |
| Inno Setup بـ`PrivilegesRequired=lowest` | مثبت بلا UAC ولا مدير؛ بيانات %APPDATA% لا تُحذف عند uninstall (لا [UninstallDelete]) |
| الإنتاج على GitHub Actions حصراً | قرار المستخدم الصريح: «البناء يكون على جيت هاب لا تبني عندك» |
| ⛔ لا GitHub Release عام | قرار المستخدم: الأرتيفاكت عبر Actions + مساحة العمل فقط |
| مثبت بالرسائل الإنجليزية | لا Arabic.isl رسمي لInno 6؛ ترجمة يدوية هشة — التطبيق نفسه عربي بالكامل |

## عقود API مهمة (لا تكسرها)

- `TeleprompterWindow(string content, AppSettings settings, Action<double> persistProgress, Action<AppSettings> settingsChanged)`
- `SettingsWindow(Window owner, SettingsStore store)` + `Action? DataCleared`
- `Dialogs.Alert/Confirm/Input` (static) — Confirm ترجع `bool?` (null = إلغاء)
- `SmokeRunner.ShouldRun(string[] args)` / `Run() → int` — exit 0 فقط عند نجاح كل الفحوص
- `StoryLibrary.NewId()/Save/ListStories/Load/Rename/Duplicate/Delete/SuggestTitle/CountWords`
- `AtomicFile.WriteAllText/TryReadAllText/TryDelete` — **كل** كتابات البيانات تمر من هنا
- env: `LATCHI_TP_DATA` (مجلد بيانات بديل — يستخدمه Smoke) و`LATCHI_SMOKE_OUT`

## مسار البيانات

```
%APPDATA%\LATCHI Teleprompter\
  settings.json        ← إعدادات (NaN bounds تُحوَّل 0 عند الحفظ — JSON لا يقبل NaN)
  autosave.json        ← آخر حالة محرر (content/title/progress)
  scripts\{id}.json    ← قصة لكل ملف؛ id = yyyymmddhhmmss+rand8 (regex مُتحقق)
  recovery\            ← كتابات الحفظ التلقائي العابرة (عند الانهيار)
```

## فخاخ معروفة (تعلمت بالتجربة)

1. **مشروع WPF المؤقت (`*_wpftmp.csproj`) لا يرث ImplicitUsings** → ملف `GlobalUsings.cs` في App يعالجها. أي ملف جديد يستخدم `System.IO/Linq` يحتاجها (متوفرة عالمياً الآن).
2. `TextBox` لا يقبل `LineHeight` في XAML بهذا السياق؛ `TextBlock` يقبلها (محرر = تباعد افتراضي، نص القراءة = 1.55×).
3. `XamlReader.Load` يريد Stream — استعمل `XamlReader.Parse` للنصوص (Dialogs.cs).
4. `System.Windows.Duration` يتعارض مع شيء في سياق wpftmp → يؤهَّل كاملاً في ScrollAnimator.
5. JSON serializer يرفض NaN/Infinity → SettingsStore يصفرّها قبل الحفظ.
6. اقتراح العنوان: إزالة **فقط** المحارف غير المرئية (FE0F/FE0E/ZWJ/200D/NonSpacingMark + surrogates) من الوسط؛ علامات الترقيم المرئية في وسط العنوان تبقى («الجزء 2: النهاية»).
7. StaticResource يجب تعريفه قبل الاستخدام في ترتيب XAML؛ `Run.Text` لا يدعم StringFormat → خصائص منسقة مسبقاً (StoryRowVm.Meta).
8. **فخ snapshot المستثنيات**: مجلدات `bin/obj/publish/dist/build` تختفي بين جلسات مساحة العمل — لا تعتمد عليها، و`git add -A` بعد فترة انقطاع قد يسجلها كحذف إن كانت متتبعة (ليست كذلك — مهملة منذ البداية).

## الإصدار (v0.1.0 — 3 أكتوبر 2026) ✅ مُسلَّم

- التشغيل الرسمي: Actions run `37138922247` على الالتزام `a75f609` (وسم `v0.1.0`) — **نجح**.
- الاختبارات: **34/34** xUnit (النواة) + **24/24** smoke على الـexe الإنتاجي نفسه في CI.
- الأرتيفاكت (الأسماء/الأحجام/الـSHA-256 حقيقية، محلية = CI):
  - `LATCHI-Teleprompter-Setup-0.1.0.exe` — 49,242,215 B — `1377ff48158eac76a0862426b9c470f95d0568e9313f60198907000ac3dc7c49`
  - `LATCHI-Teleprompter-0.1.0-portable-win-x64.zip` — 66,390,539 B — `77fdeba0a590285d21d06ba5dae383b29891ee2fc1a8705aa4a2ff8d5f91dd3e`
  - نسخة محفوظة محلياً: `/home/user/latchi-teleprompter-release/`
- ⛔ ثم ✅: نُفّذ النشر كـ**GitHub Release عام** بتاريخ 3 أكتوبر 2026 بعد طلب صريح من المستخدم نقض قاعدة المنع (release id 402656775، الأصول: Setup + Portable + manifest).
- الأيقونة: `assets/icon/latchi-teleprompter.ico` (مولدة بPIL — مستند ذهبي على كحلي).

### درس حرج من الإصدار (وُثّق لئلا يتكرر)
- **StartupUri يبتلع exit code في وضع الـsmoke**: مع `StartupUri` معرفاً، يستمر WPF بعد `Shutdown(code)` داخل OnStartup وينشئ النافذة تلقائياً → العملية تخرج بـ0. الحل النهائي: بلا StartupUri، نافذة تُنشأ يدوياً بعد فحص smoke + `ShutdownMode=OnExplicitShutdown` + `main.Closed → Shutdown()`.
- **pwsh على الrunner**: `& exe` لم يضبط `$LASTEXITCODE` (فارغ) → استخدم `Start-Process -Wait -PassThru` واقرأ `.ExitCode`.
- **فحص خاطئ التصميم يكذب**: أول تشغيل أخضر كان يحوي فحصين فاشلين (S4 ترك المحرك playing قبل فحص النهاية؛ S6 بحث عن نص غير موجود في العينة). الدرس: detail لكل فحص + طباعة smoke-results في اللوج إلزامية.

## فحص الـsmoke (ماذا يتحقق)

S1 إعدادات (افتراضيات/roundtrip/ملف تالف) · S2 مكتبة (حفظ/قائمة/تسمية/تكرار/حذف/ملف تالف معزول/اقتراح عنوان) · S3 حفظ تلقائي (debounce/flush عند dispose) · S4 محرك التمرير (تقدم/نهاية/إعادة/منحنى السرعة) · S5 محلل العلامات واتجاه النص · S6 **الواجهة الحقيقية**: إحصاءات المحرر + autosave + قياس layout + تشغيل تلقائي + تقدم فعلي + تثبيت عند الإيقاف + تغيير سرعة حي + لافتة النهاية + استمرارية التقدم.
