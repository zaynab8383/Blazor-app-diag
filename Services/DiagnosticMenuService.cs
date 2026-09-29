using System;

namespace MyBlazorApp1.Services;

public enum MenuScreen
{
    None,
    BoxPage,
    Message,
    Memo,
    SelectPage,
    Parameters,
    Sensor,
    SoftwareList
}

public class MenuItem
{
    public int Index { get; set; }

    public string Text { get; set; } = "";

    public bool Checked { get; set; }

    public bool Selected { get; set; }
}

public class MemoRecord
{
    public string Title { get; set; } = "";

    public List<string> Lines { get; set; } = new();
}

//public class DiagnosticParameter
//{
//    public int Index { get; set; }

//    public string Text { get; set; } = "";

//    public string Value { get; set; } = "";
//}


public class DiagnosticParameter
{
    public int Index { get; set; }

    public string Col0 { get; set; } = "";
    public string Col1 { get; set; } = "";
    public string Col2 { get; set; } = "";
    public string Col3 { get; set; } = "";

    // برای سازگاری با کد قبلی
    public string Value { get; set; } = "";

    // مقدار لحظه‌ای که از Type 20.2 می‌آید
    public string CurrentValue { get; set; } = "";

    public bool IsActive { get; set; }
}

public class MenuState
{
    public MenuScreen Screen { get; set; } =
        MenuScreen.None;

    public string Header { get; set; } = "";

    public string Message { get; set; } = "";
    public KeyboardState Keyboard { get; set; } = new();

    public List<MenuItem> Items { get; set; } = new();

    public List<MemoRecord> MemoRecords { get; set; } = new();

    public List<DiagnosticParameter> Parameters { get; set; } = new();
}

public class KeyboardState
{
    public bool Visible { get; set; }

    public string Header { get; set; } = "";

    public string Value { get; set; } = "";

    public string Mode { get; set; } = "Letters";

    public MenuScreen PreviousScreen { get; set; } = MenuScreen.None;
}
public class DiagnosticMenuService
{
    private readonly DiagnosticService _diagnosticService;

    public MenuState State { get; private set; } =
        new();

    public event Action? StateChanged;

    public DiagnosticMenuService(
        DiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;

        _diagnosticService.DiagnosticDataReceived +=
            OnDiagnosticDataReceived;
    }

    private void OnDiagnosticDataReceived(
        string[] value)
    {
        try
        {
            if (value == null || value.Length < 2)
                return;
            State.Keyboard.Visible = false;

            string type = value[1];

            //switch (type)
            //{


            //    case "4":
            //        ShowBoxPage(value);
            //        break;

            //    case "14":
            //        ShowMessage(value);
            //        break;

            //    case "5":
            //        ShowSelectPage(value);
            //        break;

            //    case "20":
            //        ShowParameters(value);
            //        break;

            //    case "21":
            //        ShowSensor(value);
            //        break;

            //    case "10":
            //        ShowKeyboard(value);
            //        break;

            //    case "1":
            //        ShowSoftwareList(value);
            //        break;

            //    default:
            //        break;
            //}


            switch (type)
            {
                case "4":
                    ShowBoxPage(value);
                    break;

                case "14":
                    ShowMessage(value);
                    break;

                case "5":
                    ShowSelectPage(value);
                    break;

                case "20":
                    ShowParameters(value);
                    break;

                case "20.2":
                    RefreshParameters(value);
                    break;

                case "21":
                    ShowSensor(value);
                    break;

                case "10":
                    ShowKeyboard(value);
                    break;

                case "1":
                    ShowSoftwareList(value);
                    break;

                default:
                    break;
            }


        }
        catch
        {
            // فعلاً خطای پردازش منو را نادیده می‌گیریم
        }
    }

    private void ShowSoftwareList(string[] value)
    {
        State.Screen = MenuScreen.SoftwareList;

        State.Header = "لیست نرم افزارها";

        State.Items.Clear();

        for (int i = 0; i < value.Length; i++)
        {
            if (!value[i].StartsWith("R"))
                continue;

            if (i + 1 >= value.Length)
                continue;

            string text = value[i + 1].Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            // اطلاعات سریال را هم فعلاً به عنوان متن نگه می‌داریم
            State.Items.Add(new MenuItem
            {
                Index = State.Items.Count,
                Text = text,
                Selected = false
            });
        }

        Notify();
    }
    private void ShowBoxPage(string[] value)
    {
        State.Screen = MenuScreen.BoxPage;

        State.Header = GetHeader(value);

        State.Items.Clear();

        if (value.Length < 9)
        {
            Notify();
            return;
        }

        int count = ParseInt(value[6]);

        int selectedIndex = ParseInt(value[8]);

        int counter = 9;

        for (int i = 0;
             i < count && counter + 2 < value.Length;
             i++)
        {
            string text =
                !string.IsNullOrWhiteSpace(value[counter + 2])
                    ? value[counter + 2]
                    : value[counter + 1];

            State.Items.Add(
                new MenuItem
                {
                    Index = i,
                    Text = text,
                    Selected = i == selectedIndex
                });

            counter += 3;
        }

        Notify();
    }

    private void ShowMessage(string[] value)
    {
        State.Screen = MenuScreen.Message;

        State.Header = GetHeader(value);

        State.Message =
            value.Length > 4
                ? value[4]
                : "";

        Notify();
    }

    private void ShowKeyboard(string[] value)
    {
        State.Keyboard.PreviousScreen = State.Screen;

        State.Screen = MenuScreen.None;

        State.Keyboard.Visible = true;

        State.Keyboard.Header =
            value.Length > 3
                ? value[3]
                : "";

        State.Keyboard.Value =
            value.Length > 4
                ? value[4]
                : "";

        State.Keyboard.Mode = "Letters";

        Notify();
    }

    private void ShowSelectPage(string[] value)
    {
        State.Screen = MenuScreen.SelectPage;

        State.Header = GetHeader(value);

        State.Items.Clear();

        if (value.Length < 9)
        {
            Notify();
            return;
        }

        int count = ParseInt(value[6]);

        int selectedIndex = ParseInt(value[8]);

        int counter = 9;

        for (int i = 0;
             i < count && counter + 2 < value.Length;
             i++)
        {
            string text =
                !string.IsNullOrWhiteSpace(value[counter + 2])
                    ? value[counter + 2]
                    : value[counter + 1];

            State.Items.Add(
                new MenuItem
                {
                    Index = i,
                    Text = text,
                    Checked = i == selectedIndex
                });

            counter += 3;
        }

        Notify();
    }

    //private void ShowParameters(string[] value)
    //{
    //    State.Screen = MenuScreen.Parameters;

    //    State.Header =
    //        value.Length > 3
    //            ? value[3]
    //            : "";

    //    State.Parameters.Clear();
    //    State.Items.Clear();

    //    if (value.Length < 10)
    //    {
    //        Notify();
    //        return;
    //    }

    //    int count1 = ParseInt(value[5]);

    //    int count2 = ParseInt(value[7]);

    //    int selectedIndex = ParseInt(value[9]);

    //    int counter = 10;

    //    for (int i = 0;
    //         i < count1 && counter + 4 < value.Length;
    //         i++)
    //    {
    //        State.Parameters.Add(
    //            new DiagnosticParameter
    //            {
    //                Index = i,
    //                Text = value[counter + 1],
    //                Value = value[counter + 2]
    //            });

    //        counter += 5;
    //    }

    //    for (int i = 0;
    //         i < count2 && counter + 2 < value.Length;
    //         i++)
    //    {
    //        string text =
    //            !string.IsNullOrWhiteSpace(value[counter + 2])
    //                ? value[counter + 2]
    //                : value[counter + 1];

    //        State.Items.Add(
    //            new MenuItem
    //            {
    //                Index = i,
    //                Text = text,
    //                Selected = i == selectedIndex
    //            });

    //        counter += 3;
    //    }

    //    Notify();
    //}





    private void ShowParameters(string[] value)
    {
        State.Screen = MenuScreen.Parameters;

        State.Header = "پارامترها";

        State.Parameters.Clear();

        if (value.Length < 4)
        {
            Notify();
            return;
        }

        int count = 0;

        if (value[2].Trim() == "Count")
        {
            count = ParseInt(value[3]);
        }

        int counter = 4;

        for (int i = 0; i < count; i++)
        {
            if (counter + 4 >= value.Length)
                break;

            State.Parameters.Add(
                new DiagnosticParameter
                {
                    Index = i,

                    Col0 = value[counter + 1].Trim(),
                    Col1 = value[counter + 2].Trim(),
                    Col2 = value[counter + 3].Trim(),
                    Col3 = value[counter + 4].Trim(),

                    CurrentValue = "",

                    IsActive = false
                });

            counter += 5;
        }

        Notify();
    }



    private void RefreshParameters(string[] value)
    {
        if (State.Parameters.Count == 0)
            return;

        if (value.Length < 4)
            return;

        int count = 0;

        if (value[2].Trim() == "Count")
        {
            count = ParseInt(value[3]);
        }

        foreach (var parameter in State.Parameters)
        {
            parameter.IsActive = false;
            parameter.CurrentValue = "^^^^";
        }

        int counter = 4;

        for (int i = 0; i < count; i++)
        {
            if (counter + 1 >= value.Length)
                break;

            int rowIndex = ParseInt(value[counter]);

            string currentValue =
                value[counter + 1].Trim();

            if (rowIndex >= 0 && rowIndex < State.Parameters.Count)
            {
                State.Parameters[rowIndex].CurrentValue = currentValue;
                State.Parameters[rowIndex].Value = currentValue;
                State.Parameters[rowIndex].IsActive = true;
            }

            counter += 2;
        }

        Notify();
    }

    private void ShowSensor(string[] value)
    {
        State.Screen = MenuScreen.Sensor;

        State.Header =
            value.Length > 3
                ? value[3]
                : "";

        State.Parameters.Clear();
        State.Items.Clear();

        Notify();
    }

    public void SelectMenuItem(int index)
    {
        _diagnosticService.Send(
            index.ToString());
    }

    public void SelectSensorItem(int index)
    {
        _diagnosticService.Send(
            index.ToString());
    }

    public void SetCheckBox(
        int index,
        bool checkedValue)
    {
        string command =
            $"{index},{(checkedValue ? 1 : 0)}";

        _diagnosticService.Send(command);
    }

    public void ContinueMessage()
    {
        _diagnosticService.Send("0");
    }

    public void Back()
    {
        _diagnosticService.Send("-1");
    }

    public void Back2()
    {
        _diagnosticService.Send("-2");
    }

    private static string GetHeader(
        string[] value)
    {
        if (value.Length > 4 &&
            !string.IsNullOrWhiteSpace(value[3]))
        {
            return value[3];
        }

        if (value.Length > 4)
        {
            return value[4];
        }

        return "";
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(
            value,
            out int result)
                ? result
                : 0;
    }

    private void Notify()
    {
        StateChanged?.Invoke();
    }
}