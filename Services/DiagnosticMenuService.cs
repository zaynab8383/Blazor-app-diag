using System;
using System.Collections.Generic;

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

    public string Command { get; set; } = "";

    public string Text { get; set; } = "";

    public bool Checked { get; set; }

    public bool Selected { get; set; }
}
public class MemoRecord
{
    public string Title { get; set; } = "";

    public List<string> Lines { get; set; } = new();
}


public class DiagnosticParameter
{
    public int Index { get; set; }

    public string Name { get; set; } = "";

    public string Value { get; set; } = "";

    public string CurrentValue { get; set; } = "";

    public string Col0 { get; set; } = "";

    public string Col1 { get; set; } = "";

    public string Col2 { get; set; } = "";

    public string Col3 { get; set; } = "";

    public bool IsActive { get; set; }
}


public class KeyboardState
{
    public bool Visible { get; set; }

    public string Header { get; set; } = "";

    public string Value { get; set; } = "";

    public string Mode { get; set; } = "Letters";

    public MenuScreen PreviousScreen { get; set; } =
        MenuScreen.None;
}


public class MenuState
{
    public int SoftwarePage { get; set; } = 0;
    public const int SoftwarePageSize = 18;
    public MenuScreen Screen { get; set; } =
        MenuScreen.None;

    public string Header { get; set; } = "";

    public string Message { get; set; } = "";

    public List<MenuItem> Items { get; set; } = new();

    public List<MemoRecord> MemoRecords { get; set; } = new();

    public List<DiagnosticParameter> Parameters { get; set; } = new();

    public List<DiagnosticParameter> Sensors { get; set; } = new();

    public List<MenuItem> SensorActions { get; set; } = new();

    public int SelectedIndex { get; set; } = -1;

    public KeyboardState Keyboard { get; set; } = new();
}


public class DiagnosticMenuService
{
    private readonly DiagnosticService _diagnosticService;

    public MenuState State { get; } = new();

    public event Action? StateChanged;

    public DiagnosticMenuService(
        DiagnosticService diagnosticService)
    {
        _diagnosticService =
            diagnosticService;

        _diagnosticService.DiagnosticDataReceived +=
            OnDiagnosticDataReceived;
    }


    /* ========================================================= */
    /* RECEIVE DIAGNOSTIC DATA */
    /* ========================================================= */

    private void OnDiagnosticDataReceived(
        string[] value)
    {
        try
        {
            if (value == null ||
                value.Length < 2)
            {
                return;
            }

            string type =
                value[1]?.Trim() ?? "";

            Console.WriteLine();
            Console.WriteLine(
                "========== MENU SERVICE ==========");

            Console.WriteLine(
                $"TYPE = [{type}]");

            Console.WriteLine(
                $"FIELDS = {value.Length}");

            Console.WriteLine(
                "===================================");


            switch (type)
            {
                /*
                 * Memo / information messages
                 */
                case "0":
                case "18":

                    ShowMemo(value);

                    break;


                /*
                 * Software list
                 */
                case "1":

                    ShowSoftwareList(value);

                    break;


                /*
                 * Normal box/menu page
                 */
                case "4":

                    ShowBoxPage(value);

                    break;


                /*
                 * Select / checkbox page
                 */
                case "5":

                    ShowSelectPage(value);

                    break;


                /*
                 * Keyboard
                 */
                case "10":

                    ShowKeyboard(value);

                    break;


                /*
                 * Message
                 */
                case "14":

                    ShowMessage(value);

                    break;


                case "17":
                    ShowSoftwareList(value);
                    break;

                /*
                 * Parameters
                 */
                case "20":

                    ShowParameters(value);

                    break;


                /*
                 * Parameter refresh
                 */
                case "20.2":

                    RefreshParameters(value);

                    break;


                /*
                 * Sensor
                 */
                case "21":

                    ShowSensor(value);

                    break;


                /*
                 * Sensor header
                 */
                case "21.1":

                    SetSensorHeader(value);

                    break;


                /*
                 * Sensor refresh
                 */
                case "21.3":

                    RefreshSensor(value);

                    break;


                /*
                 * Another box-page style message
                 */
                case "6":

                    ShowBoxPage(value);

                    break;


                default:

                    Console.WriteLine(
                        $"Unhandled diagnostic type: [{type}]");

                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(
                "DIAGNOSTIC MENU SERVICE ERROR");

            Console.WriteLine(ex);

            Console.WriteLine(
                "================================");
        }
    }


    private void ShowBoxPage(string[] value)
    {
        State.Screen = MenuScreen.BoxPage;

        State.Header = GetHeader(value);

        State.Items.Clear();
        State.Parameters.Clear();
        State.Sensors.Clear();
        State.SensorActions.Clear();
        State.MemoRecords.Clear();

        State.Message = "";
        State.Keyboard.Visible = false;

        if (value.Length < 9)
        {
            Notify();
            return;
        }

        int count = ParseInt(value[6]);

        int selectedIndex = ParseInt(value[8]);

        State.SelectedIndex = selectedIndex;

        int counter = 9;

        for (int i = 0; i < count; i++)
        {
            if (counter >= value.Length)
                break;

            string command = "";

            if (counter < value.Length)
            {
                command =
                    value[counter]?.Trim() ?? "";
            }

            string text = "";

            /*
             * Protocol:
             *
             * R0
             * empty
             * text
             */

            if (counter + 2 < value.Length)
            {
                text =
                    value[counter + 2]?.Trim() ?? "";
            }

            if (string.IsNullOrWhiteSpace(text) &&
                counter + 1 < value.Length)
            {
                text =
                    value[counter + 1]?.Trim() ?? "";
            }

            State.Items.Add(
                new MenuItem
                {
                    Index = i,

                    Command = command,

                    Text = text,

                    Selected =
                        i == selectedIndex
                });

            Console.WriteLine(
                $"MENU ITEM -> " +
                $"Index=[{i}], " +
                $"Command=[{command}], " +
                $"Text=[{text}]");

            counter += 3;
        }

        Console.WriteLine();
        Console.WriteLine("MENU UI UPDATE");

        Console.WriteLine(
            $"Screen = {State.Screen}");

        Console.WriteLine(
            $"Header = [{State.Header}]");

        Console.WriteLine(
            $"SelectedIndex = {State.SelectedIndex}");

        Console.WriteLine(
            $"Items = {State.Items.Count}");

        Notify();
    }


    /* ========================================================= */
    /* TYPE 5 */
    /* ========================================================= */

    private void ShowSelectPage(
        string[] value)
    {
        State.Screen =
            MenuScreen.SelectPage;

        State.Header =
            GetHeader(value);

        State.Items.Clear();

        State.Parameters.Clear();

        State.Sensors.Clear();

        State.SensorActions.Clear();

        State.MemoRecords.Clear();

        State.Message = "";

        State.Keyboard.Visible = false;


        if (value.Length < 9)
        {
            Notify();
            return;
        }


        int count =
            ParseInt(value[6]);

        int selectedIndex =
            ParseInt(value[8]);

        State.SelectedIndex =
            selectedIndex;


        int counter = 9;


        for (int i = 0;
             i < count;
             i++)
        {
            if (counter >= value.Length)
                break;


            string text = "";


            if (counter + 2 < value.Length)
            {
                text =
                    value[counter + 2]?.Trim() ?? "";
            }


            if (string.IsNullOrWhiteSpace(text) &&
                counter + 1 < value.Length)
            {
                text =
                    value[counter + 1]?.Trim() ?? "";
            }


            State.Items.Add(
                new MenuItem
                {
                    Index = i,
                    Text = text,
                    Checked =
                        i == selectedIndex,
                    Selected =
                        i == selectedIndex
                });


            counter += 3;
        }


        Notify();
    }

    ///* ========================================================= */
    ///* TYPE 0 / TYPE 18 */
    ///* MEMO */
    ///* ========================================================= */

    private void ShowMemo(
        string[] value)
    {
        State.Screen =
            MenuScreen.Memo;

        State.Header =
            GetHeader(value);

        State.MemoRecords.Clear();

        State.Items.Clear();

        State.Parameters.Clear();

        State.Sensors.Clear();

        State.SensorActions.Clear();

        State.Message = "";

        State.Keyboard.Visible = false;


        int count = 0;


        /*
         * Common structure:
         *
         * Type
         * 0
         * Count
         * N
         * ...
         */

        if (value.Length > 3 &&
            value[2] == "Count")
        {
            count =
                ParseInt(value[3]);
        }


        int counter = 4;


        for (int i = 0;
             i < count && counter < value.Length;
             i++)
        {
            string title = "";


            if (counter < value.Length)
            {
                title =
                    value[counter]?.Trim() ?? "";
            }


            counter++;


            List<string> lines =
                new();


            while (counter < value.Length)
            {
                string current =
                    value[counter]?.Trim() ?? "";


                if (current.StartsWith("R",
                    StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }


                if (!string.IsNullOrWhiteSpace(current))
                {
                    lines.Add(current);
                }


                counter++;
            }


            State.MemoRecords.Add(
                new MemoRecord
                {
                    Title = title,
                    Lines = lines
                });
        }


        Notify();
    }


    //private void ShowSoftwareList(string[] value)
    //{
    //    State.Screen = MenuScreen.SoftwareList;
    //    State.Header = "File Explorer";

    //    State.Items.Clear();
    //    State.Parameters.Clear();
    //    State.Sensors.Clear();
    //    State.SensorActions.Clear();
    //    State.MemoRecords.Clear();
    //    State.Message = "";
    //    State.Keyboard.Visible = false;

    //    int selectedIndex = -1;

    //    // -------------------------------
    //    // Selected_Index
    //    // -------------------------------
    //    for (int i = 0; i < value.Length - 1; i++)
    //    {
    //        if (value[i]?.Trim() == "Selected_Index")
    //        {
    //            selectedIndex = ParseInt(value[i + 1]);
    //            break;
    //        }
    //    }

    //    State.SelectedIndex = selectedIndex;

    //    // -------------------------------
    //    // Parse all R0, R1, R2, ...
    //    // -------------------------------
    //    for (int i = 0; i < value.Length; i++)
    //    {
    //        string field = value[i]?.Trim() ?? "";

    //        if (!field.StartsWith("R"))
    //            continue;

    //        if (!int.TryParse(field.Substring(1), out int index))
    //            continue;

    //        // Expected:
    //        //
    //        // R0
    //        // File
    //        // filename.bin

    //        if (i + 2 >= value.Length)
    //            continue;

    //        string type = value[i + 1]?.Trim() ?? "";
    //        string text = value[i + 2]?.Trim() ?? "";

    //        if (string.IsNullOrWhiteSpace(text))
    //            continue;

    //        State.Items.Add(new MenuItem
    //        {
    //            Index = index,
    //            Command = index.ToString(),
    //            Text = text,
    //            Selected = index == selectedIndex
    //        });

    //        Console.WriteLine(
    //            $"FILE ITEM -> Index=[{index}], Type=[{type}], Text=[{text}]");
    //    }

    //    // -------------------------------
    //    // Start from first page
    //    // -------------------------------
    //    State.SoftwarePage = 0;

    //    Console.WriteLine();
    //    Console.WriteLine("========== FILE EXPLORER ==========");
    //    Console.WriteLine($"Header = [{State.Header}]");
    //    Console.WriteLine($"SelectedIndex = {State.SelectedIndex}");
    //    Console.WriteLine($"Total Items = {State.Items.Count}");
    //    Console.WriteLine($"Page = {State.SoftwarePage + 1}");
    //    Console.WriteLine(
    //        $"Total Pages = {GetSoftwarePageCount()}");
    //    Console.WriteLine("===================================");

    //    Notify();
    //}




    private void ShowSoftwareList(
        string[] value)
    {
        State.Screen =
            MenuScreen.SoftwareList;

        State.Header =
            GetHeader(value);

        State.Items.Clear();

        State.Parameters.Clear();

        State.Sensors.Clear();

        State.SensorActions.Clear();

        State.MemoRecords.Clear();

        State.Message = "";

        State.Keyboard.Visible = false;


        /*
         * Software-list messages can vary slightly
         * in their field structure.
         *
         * We keep all meaningful R-fields.
         */

        int index = 0;


        for (int i = 0;
             i < value.Length;
             i++)
        {
            string field =
                value[i]?.Trim() ?? "";


            if (field.StartsWith("R",
                    StringComparison.OrdinalIgnoreCase))
            {
                string text = "";


                if (i + 2 < value.Length)
                {
                    text =
                        value[i + 2]?.Trim() ?? "";
                }


                if (string.IsNullOrWhiteSpace(text) &&
                    i + 1 < value.Length)
                {
                    text =
                        value[i + 1]?.Trim() ?? "";
                }


                if (!string.IsNullOrWhiteSpace(text))
                {
                    State.Items.Add(
                        new MenuItem
                        {
                            Index = index++,
                            Text = text
                        });
                }
            }
        }


        Notify();
    }


    public int GetSoftwarePageCount()
    {
        if (State.Items.Count == 0)
            return 0;

        return (int)Math.Ceiling(
            State.Items.Count / (double)MenuState.SoftwarePageSize);
    }

    public void SoftwarePageUp()
    {
        if (State.SoftwarePage > 0)
        {
            State.SoftwarePage--;

            Console.WriteLine();
            Console.WriteLine("========== FILE PAGE UP ==========");
            Console.WriteLine($"Page = {State.SoftwarePage + 1}");
            Console.WriteLine($"Total Pages = {GetSoftwarePageCount()}");
            Console.WriteLine("==================================");

            Notify();
        }
    }


    public void SoftwarePageDown()
    {
        int pageCount = GetSoftwarePageCount();

        if (pageCount == 0)
            return;

        if (State.SoftwarePage < pageCount - 1)
        {
            State.SoftwarePage++;
            Notify();
        }
    }


    public IEnumerable<MenuItem> GetCurrentSoftwarePageItems()
    {
        int start =
            State.SoftwarePage * MenuState.SoftwarePageSize;

        return State.Items
            .Skip(start)
            .Take(MenuState.SoftwarePageSize);
    }

    /* ========================================================= */
    /* TYPE 10 */
    /* KEYBOARD */
    /* ========================================================= */

    private void ShowKeyboard(
        string[] value)
    {
        State.Keyboard.PreviousScreen =
            State.Screen;


        State.Screen =
            MenuScreen.None;


        State.Keyboard.Visible =
            true;


        State.Keyboard.Header =
            value.Length > 3
                ? value[3]?.Trim() ?? ""
                : "";


        State.Keyboard.Value =
            value.Length > 4
                ? value[4]?.Trim() ?? ""
                : "";


        State.Keyboard.Mode =
            "Letters";


        Notify();
    }


    /* ========================================================= */
    /* TYPE 14 */
    /* MESSAGE */
    /* ========================================================= */

    private void ShowMessage(
        string[] value)
    {
        State.Screen =
            MenuScreen.Message;

        State.Items.Clear();

        State.Parameters.Clear();

        State.Sensors.Clear();

        State.SensorActions.Clear();

        State.MemoRecords.Clear();

        State.Keyboard.Visible = false;


        State.Header =
            GetHeader(value);


        /*
         * Try to use the first useful
         * field as message text.
         */

        State.Message = "";


        for (int i = 3;
             i < value.Length;
             i++)
        {
            string text =
                value[i]?.Trim() ?? "";


            if (!string.IsNullOrWhiteSpace(text) &&
                text != "Count" &&
                text != "Selected_Index")
            {
                State.Message +=
                    (State.Message.Length > 0
                        ? Environment.NewLine
                        : "")
                    + text;
            }
        }


        Notify();
    }


    /* ========================================================= */
    /* TYPE 20 */
    /* PARAMETERS */
    /* ========================================================= */

    private void ShowParameters(
        string[] value)
    {
        State.Screen =
            MenuScreen.Parameters;

        State.Header =
            GetHeader(value);

        State.Parameters.Clear();

        State.Items.Clear();

        State.Sensors.Clear();

        State.SensorActions.Clear();

        State.MemoRecords.Clear();

        State.Message = "";

        State.Keyboard.Visible = false;


        if (value.Length < 4)
        {
            Notify();
            return;
        }


        int count =
            ParseInt(value[3]);


        int counter = 4;


        for (int i = 0;
             i < count;
             i++)
        {
            if (counter >= value.Length)
                break;


            string col0 =
                counter + 1 < value.Length
                    ? value[counter + 1]?.Trim() ?? ""
                    : "";

            string col1 =
                counter + 2 < value.Length
                    ? value[counter + 2]?.Trim() ?? ""
                    : "";

            string col2 =
                counter + 3 < value.Length
                    ? value[counter + 3]?.Trim() ?? ""
                    : "";

            string col3 =
                counter + 4 < value.Length
                    ? value[counter + 4]?.Trim() ?? ""
                    : "";


            State.Parameters.Add(
                new DiagnosticParameter
                {
                    Index = i,

                    Name = col0,

                    Col0 = col0,

                    Col1 = col1,

                    Col2 = col2,

                    Col3 = col3,

                    Value = "^^^^",

                    CurrentValue = "^^^^",

                    IsActive =
                        !string.IsNullOrWhiteSpace(col0)
                });


            counter += 5;
        }


        Notify();
    }


    /* ========================================================= */
    /* TYPE 20.2 */
    /* PARAMETER REFRESH */
    /* ========================================================= */

    private void RefreshParameters(string[] value)
    {
        if (State.Parameters.Count == 0)
        {
            Notify();
            return;
        }

        int count = 0;

        if (value.Length > 3 &&
            value[2]?.Trim() == "Count")
        {
            count = ParseInt(value[3]);
        }

        Console.WriteLine();
        Console.WriteLine("========== PARAMETER PAGE REFRESH ==========");
        Console.WriteLine($"Count = {count}");
        Console.WriteLine($"Fields = {value.Length}");
        Console.WriteLine("============================================");

        /*
         * مهم:
         *
         * قبل از اعمال صفحه جدید،
         * مقدار تمام ردیف‌ها را پاک می‌کنیم.
         *
         * این دقیقاً رفتار برنامه WinForms اصلی است.
         */
        for (int i = 0; i < State.Parameters.Count; i++)
        {
            State.Parameters[i].CurrentValue = "^^^^";
            State.Parameters[i].Value = "^^^^";
            State.Parameters[i].IsActive = false;
        }

        /*
         * حالا فقط ردیف‌هایی که در پاسخ
         * 20.2 آمده‌اند مقدار می‌گیرند.
         *
         * Protocol:
         *
         * Type
         * 20.2
         * Count
         * 8
         * 0
         * value
         * 1
         * value
         * ...
         */

        int counter = 4;

        for (int i = 0; i < count; i++)
        {
            if (counter + 1 >= value.Length)
                break;

            string indexText =
                value[counter]?.Trim() ?? "";

            string newValue =
                value[counter + 1]?.Trim() ?? "";

            int index =
                ParseInt(indexText);

            Console.WriteLine(
                $"PARAMETER PAGE -> " +
                $"Index=[{index}], " +
                $"Value=[{newValue}]");

            if (index >= 0 &&
                index < State.Parameters.Count)
            {
                State.Parameters[index].CurrentValue =
                    newValue;

                State.Parameters[index].Value =
                    newValue;

                State.Parameters[index].IsActive =
                    true;
            }

            counter += 2;
        }

        Console.WriteLine();
        Console.WriteLine("========== CURRENT PARAMETER PAGE ==========");

        foreach (var parameter in State.Parameters)
        {
            Console.WriteLine(
                $"Index={parameter.Index}, " +
                $"Value=[{parameter.CurrentValue}], " +
                $"Active={parameter.IsActive}");
        }

        Console.WriteLine("============================================");

        Notify();
    }




    /* ========================================================= */
    /* TYPE 21 */
    /* SENSOR */
    /* ========================================================= */

    private void ShowSensor(
        string[] value)
    {
        State.Screen =
            MenuScreen.Sensor;

        State.Header =
            GetHeader(value);

        State.Parameters.Clear();

        State.Items.Clear();

        State.SensorActions.Clear();

        State.Sensors.Clear();

        State.MemoRecords.Clear();

        State.Message = "";

        State.Keyboard.Visible = false;


        if (value.Length < 10)
        {
            Notify();
            return;
        }


        int parameterCount =
            ParseInt(value[5]);

        int actionCount =
            ParseInt(value[7]);

        int selectedIndex =
            ParseInt(value[9]);


        State.SelectedIndex =
            selectedIndex;


        int counter = 10;


        /*
         * Sensor parameters
         *
         * Each parameter:
         *
         * Rn
         * empty
         * Col0
         * Col1
         * Col2
         */

        for (int i = 0;
             i < parameterCount;
             i++)
        {
            if (counter >= value.Length)
                break;


            string col0 =
                counter + 1 < value.Length
                    ? value[counter + 1]?.Trim() ?? ""
                    : "";

            string col1 =
                counter + 2 < value.Length
                    ? value[counter + 2]?.Trim() ?? ""
                    : "";

            string col2 =
                counter + 3 < value.Length
                    ? value[counter + 3]?.Trim() ?? ""
                    : "";

            string col3 =
                counter + 4 < value.Length
                    ? value[counter + 4]?.Trim() ?? ""
                    : "";


            State.Parameters.Add(
                new DiagnosticParameter
                {
                    Index = i,

                    Name = col0,

                    Col0 = col0,

                    Col1 = col1,

                    Col2 = col2,

                    Col3 = col3,

                    CurrentValue = col3,

                    Value = col3,

                    IsActive =
                        !string.IsNullOrWhiteSpace(col0)
                });


            counter += 5;
        }


        /*
         * Sensor action buttons
         *
         * Each action:
         *
         * Rn
         * empty
         * text
         */

        for (int i = 0;
             i < actionCount;
             i++)
        {
            if (counter >= value.Length)
                break;


            string text = "";


            if (counter + 2 < value.Length)
            {
                text =
                    value[counter + 2]?.Trim() ?? "";
            }


            if (string.IsNullOrWhiteSpace(text) &&
                counter + 1 < value.Length)
            {
                text =
                    value[counter + 1]?.Trim() ?? "";
            }


            State.Items.Add(
                new MenuItem
                {
                    Index = i,
                    Text = text,
                    Selected =
                        i == selectedIndex
                });


            State.SensorActions.Add(
                new MenuItem
                {
                    Index = i,
                    Text = text,
                    Selected =
                        i == selectedIndex
                });


            counter += 3;
        }


        State.Sensors.AddRange(
            State.Parameters);


        Notify();
    }


    /* ========================================================= */
    /* TYPE 21.1 */
    /* SENSOR HEADER */
    /* ========================================================= */

    private void SetSensorHeader(
        string[] value)
    {
        if (value.Length > 3 &&
            value[2] == "TEXT")
        {
            State.Header =
                value[3]?.Trim() ?? "";
        }


        Notify();
    }


    /* ========================================================= */
    /* TYPE 21.3 */
    /* SENSOR REFRESH */
    /* ========================================================= */

    private void RefreshSensor(
        string[] value)
    {
        if (State.Parameters.Count == 0)
        {
            Notify();
            return;
        }


        for (int i = 0;
             i < value.Length;
             i++)
        {
            string field =
                value[i]?.Trim() ?? "";


            if (!field.StartsWith("R",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }


            int index =
                ParseInt(
                    field.Length > 1
                        ? field[1..]
                        : "-1");


            if (index < 0 ||
                index >= State.Parameters.Count)
            {
                continue;
            }


            string newValue = "";


            if (i + 2 < value.Length)
            {
                newValue =
                    value[i + 2]?.Trim() ?? "";
            }


            State.Parameters[index].CurrentValue =
                newValue;

            State.Parameters[index].Value =
                newValue;

            State.Parameters[index].IsActive =
                !string.IsNullOrWhiteSpace(newValue);
        }


        State.Sensors.Clear();

        State.Sensors.AddRange(
            State.Parameters);


        Notify();
    }


    /* ========================================================= */
    /* SEND MENU COMMAND */
    /* ========================================================= */

    public async Task SelectMenuItem(
        int index)
    {
        string command =
            index.ToString();

        Console.WriteLine(
            $"MENU -> TCP [{command}]");

        await _diagnosticService.SendAsync(
            command);
    }


    /* ========================================================= */
    /* SEND SENSOR COMMAND */
    /* ========================================================= */

    public async Task SelectSensorItem(
        int index)
    {
        string command =
            index.ToString();

        Console.WriteLine(
            $"SENSOR -> TCP [{command}]");

        await _diagnosticService.SendAsync(
            command);
    }


    /* ========================================================= */
    /* CHECKBOX */
    /* ========================================================= */

    public async Task SetCheckBox(
        int index,
        bool checkedValue)
    {
        string command =
            $"{index},{(checkedValue ? 1 : 0)}";

        Console.WriteLine(
            $"CHECKBOX -> TCP [{command}]");

        await _diagnosticService.SendAsync(
            command);
    }





    // =========================================================
    // PARAMETER UP / DOWN
    // Same commands as original WinForms application
    // =========================================================

    public async Task ParameterUp()
    {
        Console.WriteLine();
        Console.WriteLine("========== PARAMETER UP ==========");
        Console.WriteLine("COMMAND = [1]");
        Console.WriteLine("==================================");

        await _diagnosticService.SendAsync("1");

        Console.WriteLine("PARAMETER UP SENT");
    }


    public async Task ParameterDown()
    {
        Console.WriteLine();
        Console.WriteLine("======== PARAMETER DOWN ==========");
        Console.WriteLine("COMMAND = [0]");
        Console.WriteLine("==================================");

        await _diagnosticService.SendAsync("0");

        Console.WriteLine("PARAMETER DOWN SENT");
    }




    /* ========================================================= */
    /* MESSAGE CONTINUE */
    /* ========================================================= */

    public async Task ContinueMessage()
    {
        await _diagnosticService.SendAsync(
            "0");
    }


    /* ========================================================= */
    /* BACK */
    /* ========================================================= */

    public async Task Back()
    {
        await _diagnosticService.SendAsync(
            "-1");
    }


    public async Task Back2()
    {
        await _diagnosticService.SendAsync(
            "-2");
    }


    /* ========================================================= */
    /* HEADER */
    /* ========================================================= */

    private string GetHeader(
        string[] value)
    {
        if (value == null ||
            value.Length < 4)
        {
            return "";
        }


        /*
         * Normal protocol:
         *
         * [0] Type
         * [1] 4
         * [2] Header
         * [3] Main Menu
         */

        if (value[2] == "Header")
        {
            return value[3]?.Trim() ?? "";
        }


        /*
         * Fallback:
         * first useful field after Type.
         */

        for (int i = 2;
             i < Math.Min(value.Length, 6);
             i++)
        {
            string text =
                value[i]?.Trim() ?? "";


            if (text == "Header")
                continue;

            if (text == "Count")
                continue;

            if (text == "Selected_Index")
                continue;


            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }


        return "";
    }


    /* ========================================================= */
    /* INTEGER PARSER */
    /* ========================================================= */

    private int ParseInt(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;


        if (int.TryParse(
            value.Trim(),
            out int result))
        {
            return result;
        }


        return 0;
    }


    /* ========================================================= */
    /* NOTIFY BLAZOR */
    /* ========================================================= */

    private void Notify()
    {
        Console.WriteLine(
            $"MENU UI UPDATE -> " +
            $"Screen={State.Screen}, " +
            $"Header=[{State.Header}], " +
            $"Items={State.Items.Count}, " +
            $"Parameters={State.Parameters.Count}");

        StateChanged?.Invoke();
    }
}