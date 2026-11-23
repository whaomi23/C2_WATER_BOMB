using Microsoft.Maui;
using Microsoft.Maui.Controls;
using System.Net;
using System.Text.Json;

namespace C2_WATER_BOMB
{
    public partial class MainPage : ContentPage
    {
        private HttpListener _listener;
        private Dictionary<string, BombaEstado> _estadoBombas = new()
        {
            ["bomba1"] = new BombaEstado(),
            ["bomba2"] = new BombaEstado()
        };
        private bool _isServerRunning;
        private CancellationTokenSource _cancellationTokenSource;

        public MainPage()
        {
            InitializeComponent();
            IniciarServidor();
        }

        private async void IniciarServidor()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _listener = new HttpListener();
                
                string url = DeviceInfo.Platform == DevicePlatform.Android ? 
                    "http://*:5000/" : "http://localhost:5000/";
                _listener.Prefixes.Add(url);
                
                _listener.Start();
                _isServerRunning = true;

                while (_isServerRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var context = await _listener.GetContextAsync();
                        _ = Task.Run(() => ProcesarPeticion(context));
                    }
                    catch (HttpListenerException)
                    {
                        // Ignorar errores de conexión
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", $"Error al iniciar el servidor: {ex.Message}", "OK");
                });
            }
        }

        private async void ProcesarPeticion(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;
                string responseString = "";

                switch (request.Url?.AbsolutePath)
                {
                    case "/estado":
                        responseString = JsonSerializer.Serialize(_estadoBombas);
                        response.ContentType = "application/json";
                        break;

                    case "/control":
                        if (request.HttpMethod == "POST")
                        {
                            using var reader = new StreamReader(request.InputStream);
                            var json = await reader.ReadToEndAsync();
                            var controlRequest = JsonSerializer.Deserialize<ControlRequest>(json);

                            if (controlRequest != null && _estadoBombas.TryGetValue(controlRequest.Bomba, out var bomba))
                            {
                                bomba.Encendida = controlRequest.Accion == "encender";
                                bomba.Tiempo = controlRequest.Tiempo;
                                response.StatusCode = 200;
                            }
                            else
                            {
                                response.StatusCode = 404;
                            }
                        }
                        break;

                    default:
                        response.StatusCode = 404;
                        break;
                }

                if (!string.IsNullOrEmpty(responseString))
                {
                    var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                response.Close();
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Error", $"Error procesando petición: {ex.Message}", "OK");
                });
            }
        }

        private void OnBomba1Toggled(object sender, ToggledEventArgs e)
        {
            ControlarBomba("bomba1", e.Value);
        }

        private void OnBomba2Toggled(object sender, ToggledEventArgs e)
        {
            ControlarBomba("bomba2", e.Value);
        }

        private void ControlarBomba(string bombaId, bool encender)
        {
            _estadoBombas[bombaId].Encendida = encender;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (encender)
                    await DisplayAlert("Éxito", $"{bombaId} encendida", "OK");
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _isServerRunning = false;
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }

        public class BombaEstado
        {
            public bool Encendida { get; set; }
            public int Tiempo { get; set; }
        }

        public record ControlRequest(string Bomba, string Accion, int Tiempo);
    }
}
