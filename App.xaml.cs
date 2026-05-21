using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using Velopack;
using WPF_Translator_Screen.Services;
using WPF_Translator_Screen.Services.API;
using WPF_Translator_Screen.Services.APIs;
using WPF_Translator_Screen.Services.Database;
using WPF_Translator_Screen.Services.OcrModals;
using WPF_Translator_Screen.Services.OcrModels;

namespace WPF_Translator_Screen
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            VelopackApp.Build().Run();

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                File.WriteAllText("error.log", ex.ExceptionObject.ToString());
            };

            base.OnStartup(e);

            var services = new ServiceCollection();

            services.AddSingleton<OcrService>();
            services.AddSingleton<TranslationService>();
            services.AddSingleton<AzureOutBoundService>();
            services.AddSingleton<GoogleOutBoundService>();
            services.AddSingleton<PaddleOcr>();
            services.AddSingleton<TesseractOcr>();
            services.AddSingleton<OllamaService>();
            services.AddSingleton<SQLiteService>();
            services.AddSingleton<TranslationOutBoundService>();

            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            var syncService = Services.GetRequiredService<TranslationOutBoundService>();
            syncService.Start();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}