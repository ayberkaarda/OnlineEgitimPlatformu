# Değişiklik Günlüğü

🇬🇧 [English](CHANGELOG.md) · 🇹🇷 **Türkçe**

Bu projedeki önemli değişiklikler bu dosyada belgelenir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) temel alınarak hazırlanmıştır.

## [Yayımlanmadı]

### Eklenenler

- `%LOCALAPPDATA%\OnlineEgitim\logs` altında Serilog ile günlük dönen dosya günlükleri (30 günden eski tarihli
  dosyalar silinir, en fazla 30 dosya saklanır; testler bunu tarihli dosyalarla doğrular). Adlar, e-posta adresleri,
  şifreler, kurtarma kodları, kurulum anahtarları, not içerikleri ve istisna mesajları hiçbir zaman günlüğe yazılmaz.
- Denetim kayıtları günlük dosyasına sabit bir kod ve rol olarak yazılır, örneğin `Audit auth.sign_in.failed student`;
  her başarısız giriş aynı kaydı bırakır.
- Kayıt, giriş, ilk kurulum, ilk şifre, kurtarma kodu, kurs ve not için merkezi FluentValidation doğrulayıcıları.
- Doğrulayıcı sınır testleri, kurtarma ve kurulum yolları ile servis bağlantıları için katı Moq testleri, reddedilen
  kurs, not ve kurtarma girdisinin hiçbir şey yazmadığını gösteren SQLite testleri ve tam bir oturumu çalıştırıp
  dosyada kişisel veri, anahtar ve kod arayan günlük testleri.
- SQL Server testi `SetupKey_ExpiresWhileWaiting_IsRefused` ve politika testi
  `SetupKey_EnrolledStudent_IsRefusedAndKeyStaysUnused`.
- CI'da sürüm işi: `v*` etiketlerinde etiket adı doğrulanır, win-x64 derlemesi yayımlanır ve zip olarak yüklenir.
- Yetki matrisi ve mimari diyagramı içeren İngilizce ve Türkçe README, tehdit modeli (`docs/THREAT_MODEL.md`) ve bu
  değişiklik günlüğü.
- Daha önce, temel düzenleme çalışmasında: EF Core migration'ları ve DI ile katmanlı `net10.0` projeleri (Domain,
  Business, Data, UI); PBKDF2 şifre özetleri ve hesap kilidi; her serviste rol ve sahiplik denetimi; veritabanı
  sahibinin kurulum anahtarıyla ilk kurulum (`docs/issue-setup-key.sql`, `docs/grant-app-login.sql`); eski
  uygulamanın şifresiz bıraktığı hesapların kurtarılması; eski veritabanının yerinde yükseltilmesi; isteğe bağlı
  demo verisi; özgün görseller; xUnit v3 testleri ve CI derlemesi.

### Değişenler

- Kendine özgü kimlikle yeniden tasarlanan arayüz: yumuşak mor-gri nötrler üzerinde koyu çivit-mor vurgu ve sıcak
  güneş sarısı işaret, `assets/logo.svg` dosyasından üretilen yeni logo (yükselen çubuklu bir ekran ve bir kıvılcım) ve
  "Online Eğitim" adı. Her ekran merkezi tema değerleriyle tek bir çerçeve üzerine yeniden kuruldu (başlık, satır içi
  ileti şeridi, kartlar, alt bilgi eylemleri); giriş ekranlarında marka paneli var. Sonuçlar, doğrulama ve kural
  retleri pencere içinde görünür; sorular temalı bir ileti penceresinde sorulur. Listeler iki satırdır ve uzun bir
  adın gizleyemediği bir durum etiketi taşır (kayıtlı, dolu, kapalı, boş yer, devre dışı, şifresiz); yüklenirken ve
  boşken bunu söyler. Tek kullanımlık kodlar büyük, eş aralıklı bir alanda bir kez gösterilir. Açık, koyu
  (`Appearance:Theme`) ve Windows yüksek karşıtlık paletleri; yüksek karşıtlık değişikliği uygulama açıkken izlenir.
  Monitör başına DPI, çalışma alanıyla sınırlı ve içeriği kaydırılan pencereler, her ekranda klavye sırası ve Esc.
  Öğrenci ve öğretmen panelinden oturum kapatılabilir; şifre değişince giriş ekranı oturumun neden kapandığını söyler.
- `tools/Screenshots`, README ekran görüntülerini uydurma bir okulla pencere açmadan çizer ve simgeyi üretir.
- İlk kurulum ve kurulum anahtarı ilk öğretmen yerine ilk yöneticiyi oluşturur; şifreyle giriş yapan bir öğretmen
  veya yönetici varken kurulum anahtarı reddedilir.
- Öğretmenin kurtarma kodu şifresi olan öğrenci için de çalışır (unutulan şifre); personel hesapları da "Kodla şifre
  belirle" ekranında kodla şifre belirler.
- Öğretmen kilidini beklerken süresi dolan kurulum anahtarı testi deterministiktir: denemenin zamanı sabittir ve
  anahtarın süresi kilit tutulurken veritabanında doldurulur.

- Servislerde elle yazılmış girdi denetimleri doğrulayıcılara taşındı; mesajlar, kırpma ve denetimlerin sırası aynı.
- İzleme çıktısına (trace) yazılan denetim olayları ve hatalar artık günlük dosyasına yazılır; reddedilen işlemler
  yalnızca istisna türünü yazar.
- Kurulum anahtarı bir öğrenciyi yalnızca o öğrenci hiçbir derse kayıtlı değilken kurtarır; derse kayıtlı öğrenci
  kurtarma kodunu dersin öğretmeninden alır. Kurtarma ekranı, servis belgeleri ve README'ler bunu söyler.
- README İngilizce `README.md` ve Türkçe `README.tr.md` olarak ayrıldı; katlanabilir İngilizce bölüm kaldırıldı.

### Düzeltilenler

- Hesap ya da sıfırlama kodu üretilirken yönetici paneli kapatılırsa tek kullanımlık kod kayboluyordu; kapatma artık
  işlemi bekler ve kod okunana kadar pencere açık kalır.
- Uygulama kilidi beklemeleri (kurs, yöneticiler, yükseltme) "meşgul, tekrar deneyin" yerine 30 saniyelik komut zaman
  aşımıyla bitebiliyordu; kilit komutu artık beklemesinden uzun sürebilir ve reddedilen kilit "İşlem meşgul" der.
- Veritabanında 1000'den büyük kontenjanı olan bir kursu düzenlemek hata veriyordu; alan sınırda, bir uyarıyla açılır.
- Uygulama login'i, öğretmenler şifreyle giriş yaparken hiç yönetici yoksa doğrudan SQL ile yönetici ekleyebiliyordu;
  `TR_Egitmenler_Rol` bunu da reddeder (migration `RefuseAdministratorBesideStaffPasswords`). README, tehdit modeli ve
  demo modu bölümü bunu, kodun ekranda ne kadar kaldığını ve demo veritabanının yöneticisi için
  `docs/promote-admin.sql` gerektiğini anlatır.
- Yeni kayıt ile kullanılan ad veya e-postayla yapılan kayıt, kullanıcının okuyabildiği günlük dosyasına farklı
  denetim kodları yazıyordu; ikisi de artık `auth.student.registration_submitted` yazar.

- Öğretmen kilidini beklerken süresi dolan bir kurulum anahtarı yine de kabul ediliyordu. Anahtarı tüketen son
  `UPDATE` artık süreyi veritabanının saatine (`SYSUTCDATETIME()`) karşı denetler: böyle bir anahtar reddedilir,
  şifre belirlenmez ve anahtar kullanılmamış kalır.
- Kurulum anahtarı, bir derse kayıtlı şifresiz bir öğrencinin ilk şifresini belirliyordu. Artık yanlış anahtar gibi
  reddedilir ve kullanılmamış kalır.
