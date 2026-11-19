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

- Yeni kayıt ile kullanılan ad veya e-postayla yapılan kayıt, kullanıcının okuyabildiği günlük dosyasına farklı
  denetim kodları yazıyordu; ikisi de artık `auth.student.registration_submitted` yazar.

- Öğretmen kilidini beklerken süresi dolan bir kurulum anahtarı yine de kabul ediliyordu. Anahtarı tüketen son
  `UPDATE` artık süreyi veritabanının saatine (`SYSUTCDATETIME()`) karşı denetler: böyle bir anahtar reddedilir,
  şifre belirlenmez ve anahtar kullanılmamış kalır.
- Kurulum anahtarı, bir derse kayıtlı şifresiz bir öğrencinin ilk şifresini belirliyordu. Artık yanlış anahtar gibi
  reddedilir ve kullanılmamış kalır.
