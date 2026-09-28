using System.IO.Ports;

namespace MyBlazorApp1.Services;

public class SerialService : IDisposable
{
    private SerialPort? _serialPort;

    public bool IsConnected => _serialPort?.IsOpen == true;

    public event Action<string>? DataReceived;

    public string[] GetPorts()
    {
        return SerialPort.GetPortNames();
    }

    public void Connect(string portName, int baudRate)
    {
        if (IsConnected)
            return;

        _serialPort = new SerialPort(
            portName,
            baudRate,
            Parity.None,
            8,
            StopBits.One);

        _serialPort.DataReceived += SerialPort_DataReceived;

        _serialPort.Open();
    }

    private void SerialPort_DataReceived(
        object sender,
        SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                return;

            int bytesToRead = _serialPort.BytesToRead;

            if (bytesToRead <= 0)
                return;

            byte[] buffer = new byte[bytesToRead];

            _serialPort.Read(
                buffer,
                0,
                buffer.Length);

            string hex = BitConverter
                .ToString(buffer)
                .Replace("-", " ");

            DataReceived?.Invoke(hex);
        }
        catch
        {
            // فعلاً چیزی انجام نمی‌دهیم
        }
    }

    public void Send(byte[] data)
    {
        if (!IsConnected)
            throw new InvalidOperationException(
                "Serial port is not connected.");

        string hex = BitConverter
            .ToString(data)
            .Replace("-", " ");

        Console.WriteLine("========== SERIAL SEND ==========");
        Console.WriteLine($"HEX: {hex}");
        Console.WriteLine($"ASCII: {System.Text.Encoding.UTF8.GetString(data)}");
        Console.WriteLine("=================================");

        _serialPort!.Write(data, 0, data.Length);
    }
    public void Disconnect()
    {
        if (_serialPort != null)
        {
            _serialPort.DataReceived -= SerialPort_DataReceived;

            if (_serialPort.IsOpen)
                _serialPort.Close();

            _serialPort.Dispose();
            _serialPort = null;
        }
    }

    public void Dispose()
    {
        Disconnect();
    }
}