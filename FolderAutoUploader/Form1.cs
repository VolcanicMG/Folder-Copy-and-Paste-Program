﻿using FolderAutoUploaderMain;
using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;

namespace FolderAutoUploader
{
    public partial class Form1 : Form
    {
        public List<DateTime> dates = new List<DateTime>();
        public DateTime _dateLastRan;

        public Data.checkCycle _checkType;
        public bool _alreadyRanToday;
        public bool _alreadyRanThisWeek;
        public bool _alreadyRanThisMonth;

        private DateTime _dailyTime;
        private DateTime _weeklyTime;
        private DateTime _monthlyTime;

        public string uploadPath;
        public string replacePath;

        public bool overwriteFiles;

        public static bool workin;
        
        private bool switched;

        public static System.Windows.Forms.Timer timer;

        public static int _folderLimit;

        //Used for the schedule form
        public static List<LocationsAdd> _scheduleLocations;
        public static List<ProcessHelper> _processes;

        //Settings
        public bool _loadOnStartup;
        public bool _runFromBackground;
        public bool _runMinimized;

        //Build Info
        public string currentVersion { get; private set; }

        public Form1(bool runMinimized)
        {
            InitializeComponent();

            _runMinimized = runMinimized;

            this.Text += $" | Version: V{Application.ProductVersion}";

            //Set tool-tips
            toolTip1.SetToolTip(this.label2, "Pick a folder to override another");
            toolTip1.SetToolTip(this.label3, "Pick a folder that gets overwritten");
            toolTip1.SetToolTip(this.overwriteCheckbox, "Deletes and Creates a new folder with the same name");
            toolTip1.SetToolTip(this.datePickerButton, "*WIP*");

            //Rich text box
            loggerInfo.HideSelection = false;

            //set and reset
            _scheduleLocations = new List<LocationsAdd>();

            //Check to make sure the directory is created
            if (!ProcessHelper.CheckDirectory(Info.folderName + @"\CopyPasteData"))
            {
                Directory.CreateDirectory(Info.folderName + @"\CopyPasteData");
            }

            //Set to blank so we don't have to see it
            fileInfoLabel.Text = "";
            folderInfoLabel.Text = "";

            //Set the calendar to not precede today
            dateTimePicker.MinDate = DateTime.Today;

            _processes = new List<ProcessHelper>();

            //Load in the dates file and check it
            if (File.Exists(Info.datesFileLocation))
            {
                IFormatter formatter = new BinaryFormatter();

                Stream stream = new FileStream(Info.datesFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read);
                Data infoData = (Data)formatter.Deserialize(stream);
                stream.Close();

                dates = infoData.dates;
                _checkType = infoData.checktype;
                _alreadyRanToday = infoData.alreadyRanToday;
                _alreadyRanThisWeek = infoData.alreadyRanThisWeek;
                _alreadyRanThisMonth = infoData.alreadyRanThisMonth;
                _weeklyTime = infoData.weeklyTime;
                _monthlyTime = infoData.monthlyTime;
                _dateLastRan = infoData.dateLastRan;
                _folderLimit = infoData.folderLimit;
                _scheduleLocations = infoData.scheduleLocations;
                _loadOnStartup = infoData.loadOnStartup;
                _runFromBackground = infoData.runFromBackground;
                _dailyTime = infoData.dailyTime;

                //Used for the first time or when there is nothing
                if (_scheduleLocations == null)
                {
                    _scheduleLocations = new List<LocationsAdd>();
                }
            }
            else
            {
                //Print("Dates file not created yet (Will happen on first ever launch or if the file gets deleted)"); //WILL CAUSE ERROR ATM. MOVE TO LOAD?
            }

            //Set numeric 
            if (numericUpDown1.Value <= 0)
            {
                numericUpDown1.Value = 3;
            }

            if(_folderLimit < 2)
            {
                _folderLimit = 3;
            }

            //Set the value of the incriminate
            if (_folderLimit != null && _folderLimit > 0)
            {
                numericUpDown1.Value = _folderLimit;
            }
            else numericUpDown1.Value = 3;

            numericUpDown1.Maximum = 50;
            numericUpDown1.Minimum = 1;

            DateTime today = DateTime.Today;

            //Check and see if the program already ran today and if not set it to false
            if (_alreadyRanToday)
            {
                if (_dateLastRan <= today && _dateLastRan != DateTime.MinValue)
                {
                    _alreadyRanToday = false;
                }
            }

            if (_alreadyRanThisWeek)
            {
                if (today >= _weeklyTime)
                {
                    _alreadyRanThisWeek = false;
                }
            }

            if (_alreadyRanThisMonth)
            {
                if (today >= _monthlyTime)
                {
                    _alreadyRanThisMonth = false;
                }
            }

            Debug.WriteLine("Loaded in daily time: " + _dailyTime);

            //Load with the correct buttons checked and cycle settings
            switch (_checkType)
            { 
                case Data.checkCycle.dailyChecked:
                    dailyRadioButton.Checked = true;
                    datePickerButton.Enabled = false;
                    dateTimePicker.Enabled = false;

                    if(_dailyTime == null)
                    {
                        _dailyTime = DateTime.Now.AddDays(1);
                    }
                    break;

                case Data.checkCycle.weeklyChecked:
                    weeklyRadioButton.Checked = true;
                    datePickerButton.Enabled = false;
                    dateTimePicker.Enabled = false;

                    if (_weeklyTime == null)
                    {
                        _weeklyTime = today.AddDays(7);
                    }
                    break;

                case Data.checkCycle.monthlyChecked:
                    monthlyRadioButton.Checked = true;
                    datePickerButton.Enabled = false;
                    dateTimePicker.Enabled = false;

                    if (_monthlyTime == null)
                    {
                        _monthlyTime = today.AddMonths(1);
                    }
                    break;

                case Data.checkCycle.notChecked:
                    noneRadioButton.Checked = true;

                    _dailyTime = DateTime.Now;
                    _weeklyTime = DateTime.Now;
                    _monthlyTime = DateTime.Now;
                    break;

                default:
                    noneRadioButton.Checked = true;

                    _dailyTime = DateTime.Now;
                    _weeklyTime = DateTime.Now;
                    _monthlyTime = DateTime.Now;
                    break;
            }

            Debug.WriteLine("Daily Time start: " + _dailyTime);

            //Load in the dates that are already selected
            foreach (DateTime date in dates)
            {
                //Add the text into the date
                dateTextBox.AppendText(date + "\n");
            }

            //on exit save all the dates
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnExit);

            //ProcessHelper.Print($"Already ran today: {_alreadyRanToday}. The Date last ran: {_dateLastRan}");
        }


        #region Delcared_Functions

        /// <summary>
        /// Used to save the dates and other info that needs saved
        /// </summary>
        private void Save()
        {
            //Save the selected
            for (int i = 0; i < checkedListBoxLocations.Items.Count; i++)
            {
                _scheduleLocations[i].selected = checkedListBoxLocations.GetItemChecked(i);
            }

            BinaryFormatter formatter = new BinaryFormatter();
            Data infoData = new Data();

            infoData.dates = dates;
            infoData.checktype = _checkType;
            infoData.alreadyRanToday = _alreadyRanToday;
            infoData.alreadyRanThisWeek = _alreadyRanThisWeek;
            infoData.alreadyRanThisMonth = _alreadyRanThisMonth;
            infoData.weeklyTime = _weeklyTime;
            infoData.monthlyTime = _monthlyTime;
            infoData.dateLastRan = _dateLastRan;
            infoData.folderLimit = _folderLimit;
            infoData.scheduleLocations = _scheduleLocations;
            infoData.loadOnStartup = _loadOnStartup;
            infoData.runFromBackground = _runFromBackground;
            infoData.dailyTime = _dailyTime;
            
            //Should add an try catch here at some point (And all the times we write and open)****************************************--------------------------*****************************************
            using (FileStream stream = File.OpenWrite(Info.datesFileLocation))
            {
                formatter.Serialize(stream, infoData);
                stream.Close();
            }
        }

        /// <summary>
        /// Used to run functions at the end of the program (When it closes)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnExit(object sender, EventArgs e)
        {

            if (ProcessHelper.CheckDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CopyPasteData"))
            {
                Save();
            }
            else
            {
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\CopyPasteData");

                Save();
            }

            //If we have nothing checking and nothing in the dates there is no reason to start the background process
            if (_checkType != Data.checkCycle.notChecked && dates.Count <= 0)
            {
                Process.Start(EXEPaths.backgroundEXEPath, "-Start");
            }
        }

        /// <summary>
        /// Enable or disable of the buttons
        /// </summary>
        /// <param name="on"></param>
        public void EnableButtons(bool on)
        {
            Invoke(new Action(() =>
            {
                //Disable the buttons once the program starts
                uploadButton.Enabled = on;
                SaveButton.Enabled = on;
                LoadButton.Enabled = on;

                overwriteCheckbox.Enabled = on;

                searchButton1.Enabled = on;
                searchButton2.Enabled = on;

                copySelectedButton.Enabled = on;
            }));

        }


        /// <summary>
        /// Reset the buttons to make sure everything can be used again
        /// </summary>
        public void Reset()
        {

            Invoke(new Action(() =>
            {
                EnableButtons(true);

                progressBar1.Value = 0;
                progressBar2.Value = 0;

                fileInfoLabel.Text = "";
                folderInfoLabel.Text = "";
                runningLabel.Text = "";
            }));

        }

        /// <summary>
        /// Used to save the upload and replace path in a text folder for later reading
        /// </summary>
        private void saveToFile()
        {
            //reset
            File.WriteAllText(Info.saveFileLocation, "");

            using (StreamWriter sw = File.AppendText(Info.saveFileLocation))
            {
                sw.WriteLine(uploadLocationString.Text);
                sw.WriteLine(replaceLocationString.Text);

                sw.Close();
            }

            Print("Upload and replace path have been saved");
        }

        /// <summary>
        /// Used to specify the event that happens once the timer runs out and the user has automatic startup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void RunStartup()
        {
            DateTime now = DateTime.Now;

            Invoke(new Action(() =>
            {
                //Change the label to running
                runningLabel.Text = "Running...";

                button1_Click_1();

                Print("Starting...");

                _dateLastRan = now;

            }));

        }

        #endregion

        //Used to start an copy action
        public void button1_Click(object sender = null, EventArgs e = null)
        {
            Print("-------------------NEW PROCESS------------------------");

            //Checking to make sure we are not loading in the same folder
            if (replaceLocationString.Text.Equals(uploadLocationString.Text) || ProcessHelper.CheckDirectoryContain(uploadLocationString.Text, replaceLocationString.Text))
            {
                Print("Locations are the same or you are in the same directory as the copy location");
                return;
            }

            ProcessHelper copy = new ProcessHelper(this, true, 0, uploadLocationString.Text, replaceLocationString.Text, overwriteFiles, _folderLimit, progressBar1, progressBar2, folderInfoLabel, fileInfoLabel);
            copy.Start();
        }

        private void Upload_Click(object sender, EventArgs e)
        {

        }

        private void progressBar1_Click(object sender, EventArgs e)
        {

        }

        private void monthCalendar1_DateChanged(object sender, DateRangeEventArgs e)
        {

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void DatePicker_Click(object sender, EventArgs e)
        {
            //ProcessHelper.Print the date in the logger
            //ProcessHelper.Print(dateTimePicker.Text);

            if (!dates.Contains(dateTimePicker.Value))
            {
                //Add to the list
                dates.Add(dateTimePicker.Value);

                //Add the text into the date
                dateTextBox.Text += dateTimePicker.Text + "\n";
            }
            else
            {
                Print("Already in the list");
            }

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void loggerInfo_TextChanged(object sender, EventArgs e)
        {

        }

        private void searchButton1_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string folder = folderBrowserDialog1.SelectedPath;

                //check to make sure the directory exists
                if (ProcessHelper.CheckDirectory(folder))
                {
                    uploadPath = folder;

                    uploadLocationString.Text = folder;
                }
            }
        }

        private void searchButton2_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog(); // Show the dialog.
            if (result == DialogResult.OK) // Test result.
            {
                string folder = folderBrowserDialog1.SelectedPath;

                //check to make sure the directory exists
                if (ProcessHelper.CheckDirectory(folder))
                {
                    replacePath = folder;

                    replaceLocationString.Text = folder;
                }
            }
        }

        private void toolTip1_Popup(object sender, PopupEventArgs e)
        {

        }

        private void overwriteCheckbox_CheckedChanged(object sender, EventArgs e)
        {

            overwriteFiles = overwriteCheckbox.Checked;

            if (overwriteCheckbox.Checked)
            {
                uploadButton.Text = "Overwrite";
            }
            else uploadButton.Text = "Copy";
        }

        private void loggerClearButton_Click(object sender, EventArgs e)
        {
            //reset
            loggerInfo.Text = "";

            progressBar1.Value = 0;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (!uploadPath.Equals("") || !replacePath.Equals(""))
            {

                //if the file don't exist create it
                if (!File.Exists(Info.saveFileLocation))
                {
                    using (StreamWriter sw = File.CreateText(Info.saveFileLocation))
                    {

                    }

                    Print("Save file created");
                }

                //save to the file
                saveToFile();
            }
            else Print("Error - One or more files do not exist or have not been selected");
        }

        private void LoadButton_Click(object sender = null, EventArgs e = null)
        {
            if (File.Exists(Info.saveFileLocation))
            {
                string[] lines = File.ReadAllLines(Info.saveFileLocation);

                if (lines.Length > 0)
                {
                    uploadPath = lines[0];
                    replacePath = lines[1];

                    Invoke(new Action(() =>
                    {
                        uploadLocationString.Text = uploadPath;
                        replaceLocationString.Text = replacePath;

                    }));

                    Print("Loaded in paths");
                }
            }
            else Print("Nothing to load");

        }

        /// <summary>
        /// Logger
        /// </summary>
        /// <param name="print"></param>
        public void Print(object print)
        {

            Invoke(new Action(() =>
            {
                try
                {
                    loggerInfo.AppendText("[" + DateTime.Now + "] " + print.ToString() + "\n");
                }
                catch (Exception e)
                {

                }
            }));

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void canclelButton_Click(object sender, EventArgs e)
        {
            if (workin)
            {
                workin = false;

                Print("Canceling");
            }
            else Print("No process is running");
        }

        private void clearDatesButton_Click(object sender, EventArgs e)
        {
            dates.Clear();

            dateTextBox.Text = "";
        }

        private void dateTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        //so if the Internet is out the client won't crash on loading
        private bool CheckForInternetConnection()
        {
            try
            {
                using (var client = new WebClient())
                using (client.OpenRead("http://google.com/generate_204"))
                    client.Dispose();
                    return true;
            }
            catch
            {
                return false;
            }
        }

        private void CheckVersion()
        {
            //Goes out and grabs the version that the mod browser has
            using (WebClient client = new WebClient())
            {
                if (CheckForInternetConnection())
                {
                    //Parsing the data we need
                    try
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        var json = client.DownloadString("https://raw.githubusercontent.com/VolcanicMG/Folder-Copy-and-Paste-Program/main/Version.txt");
                        json.ToString().Trim();
                        currentVersion = json;
                        
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e);
                    }
                    finally
                    {
                        client.Dispose();
                    }
                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cancelButton.Enabled = false;

            windowsStartupCheckBox.Checked = _loadOnStartup;
            autoRunCheckBox.Checked = _runFromBackground;

            //Fill in the cells for the locations
            if (_scheduleLocations != null && _scheduleLocations.Count > 0)
            {
                foreach (LocationsAdd location in _scheduleLocations)
                {
                    checkedListBoxLocations.Items.Add(location.name, location.selected);
                }
            }

            //Get and check the version from github
            CheckVersion();

            //Checking to make sure we are on the right version
            if (currentVersion == null)
            {
                Print("There is no Internet connection");
            }
            else if (!Application.ProductVersion.Equals(currentVersion))
            {
                Print("You are not on the current version, please check the github for the latests version");

                //Display the version
                Print($"Web Version: V{currentVersion} Local Version: {Application.ProductVersion}");
            }

            //Once the program starts from the background run, and run minimized
            if(_runFromBackground && _runMinimized)
            {
                RunStartup();

                this.WindowState = FormWindowState.Minimized;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            _folderLimit = (int)numericUpDown1.Value;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        //Add Button
        private void button2_Click(object sender, EventArgs e)
        {
            AddLocationsForm addLocationsForm = new AddLocationsForm();
            DialogResult dialogResult = addLocationsForm.ShowDialog();

            //Refresh the list to make all the new changes appear
            checkedListBoxLocations.Items.Clear();

            //Fill in the cells for the locations
            if (_scheduleLocations != null && _scheduleLocations.Count > 0)
            {
                foreach (LocationsAdd location in _scheduleLocations)
                {
                    checkedListBoxLocations.Items.Add(location.name, location.selected);
                }
            }
        }

        //Copy All Selected Button
        private void button1_Click_1(object sender = null, EventArgs e = null)
        {
            //Change the button and what it does when pressed
            if (!switched && checkedListBoxLocations.CheckedItems.Count > 0 && !workin)
            {
                switched = true;
                copySelectedButton.Text = "Cancel";
            }
            else if (switched)
            {
                switched = false;
                workin = false;
                copySelectedButton.Text = "Copy Selected";

                return;
            }

            if (checkedListBoxLocations.CheckedItems.Count > 0 && !workin && switched)
            {
                int id = 0;

                foreach (string LocationName in checkedListBoxLocations.CheckedItems)
                {
                    LocationsAdd findCheckedLocation = _scheduleLocations.Find(x => x.name.Contains(LocationName));
                    ProcessHelper copy = new ProcessHelper(this, false, id, findCheckedLocation.copy, findCheckedLocation.replace, overwriteFiles, _folderLimit, progressBar1, progressBar2, folderInfoLabel, fileInfoLabel);

                    id++;
                }

                //start
                _processes[0].Start();

                //Debug
                //Print("Processes: " + _processes.Count.ToString());
                //foreach (ProcessHelper prcess in _processes)
                //{
                //    Print("\n");

                //    prcess.countFiles(prcess.uploadPath);

                //    Print("Process ID: " + prcess.workerID.ToString());
                //    Print("UploadPath: " + prcess.uploadPath.ToString());
                //    Print("ReplacePath: " + prcess.replacePath.ToString());
                //    Print("Amount of files " + prcess.actualFiles);

                //}
            }
            else
            {
                Print("You have no locations selected or set up");
            }
        }

        public void NextProcess()
        {
            //0 is the current process being processed
            if (_processes.Count > 0)
            {
                Print("-------------------NEW PROCESS------------------------");
                _processes[0].Start();
            }
            else
            {
                //After running add time to the weekly and monthly timer
                switch (_checkType)
                {
                    case Data.checkCycle.notChecked:

                        break;

                    case Data.checkCycle.dailyChecked:
                        _alreadyRanToday = true;
                        break;

                    case Data.checkCycle.weeklyChecked:
                        _alreadyRanThisWeek = true;
                        _weeklyTime =  DateTime.Now.AddDays(7);
                        break;

                    case Data.checkCycle.monthlyChecked:
                        _alreadyRanThisMonth = true;
                        _monthlyTime = DateTime.Now.AddMonths(1);
                        break;
                }

                Print("All done with processes");

                //Make sure we reset the copy button
                switched = true;

                //Invoke the process that first had the text and tell it to change
                Invoke(new Action(() =>
                {
                    copySelectedButton.Text = "Copy Selected";

                    Reset();
                }));
            }
        }

        //Remove Button
        private void RemoveButton_Click(object sender, EventArgs e)
        {
            //Delete it from the static list
            foreach (string Location in checkedListBoxLocations.CheckedItems)
            {
                //Remove from the actual list too
                _scheduleLocations.Remove(_scheduleLocations.Find(x => x.name.Contains(Location)));
            }

            //Get the count
            int CheckedCount = checkedListBoxLocations.CheckedItems.Count;

            //Delete it from the list UI
            for (int i = 0; i < CheckedCount; i++)
            {
                ((IList)checkedListBoxLocations.Items).Remove(checkedListBoxLocations.CheckedItems[0]);
            }
        }

        private void tabPage3_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click_1(object sender, EventArgs e)
        {

        }

        //Get the current selected one?
        private void checkedListBoxLocations_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void windowsStartupCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _loadOnStartup = windowsStartupCheckBox.Checked;

            //Check and add the the startup key
            if (_loadOnStartup)
            {
                //Use a double try/catch to make sure the program does not crash and so we can use the finally statement
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\currentVersion\\Run", true);

                    try
                    {

                        key.SetValue("Auto_Copy/Paste_Program", "\"" + Application.ExecutablePath + "\"");
                        key.Close();
                    }
                    finally
                    {
                        key.Dispose();
                    }
                }
                catch (Exception ee)
                {
                    Print(ee);
                }
            }
            else
            {
                //Use a double try/catch to make sure the program does not crash and so we can use the finally statement
                try
                {
                    RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\currentVersion\\Run", true);

                    try
                    {

                        key.SetValue("Auto_Copy/Paste_Program", "\"" + Application.ExecutablePath + "\"");
                        key.DeleteValue("Auto_Copy/Paste_Program", false);
                        key.Close();
                    }
                    finally
                    {
                        key.Dispose();
                    }
                }
                catch (Exception ee)
                {
                    Print(ee);
                }
            }
        }

        private void dateAndTimesText_Click(object sender, EventArgs e)
        {

        }

        private void AutoRunCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if(autoRunCheckBox.Checked)
            {
                _runFromBackground = true;
            } 
            else _runFromBackground = false;
        }

        //Radio 
        private void monthlyRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (monthlyRadioButton.Checked)
            {
                _checkType = Data.checkCycle.monthlyChecked;
                datePickerButton.Enabled = false;
                dateTimePicker.Enabled = false;

                _monthlyTime = DateTime.Now.AddMonths(1);
            }
        }

        private void weeklyRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (weeklyRadioButton.Checked)
            {
                _checkType = Data.checkCycle.weeklyChecked;
                datePickerButton.Enabled = false;
                dateTimePicker.Enabled = false;

                _weeklyTime = DateTime.Now.AddDays(7);

            }
        }

        private void dailyRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (dailyRadioButton.Checked)
            {

                _checkType = Data.checkCycle.dailyChecked;
                datePickerButton.Enabled = false;
                dateTimePicker.Enabled = false;

                
                //_dailyTime = DateTime.Now.AddDays(1);
                _dailyTime = DateTime.Now.AddDays(1);

                Debug.WriteLine($"New time: {_dailyTime}");
            }
        }

        private void noneRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (noneRadioButton.Checked)
            {
                _checkType = Data.checkCycle.notChecked;
                datePickerButton.Enabled = true;
                dateTimePicker.Enabled = true;

                _dailyTime = DateTime.Now;
                _weeklyTime = DateTime.Now;
                _monthlyTime = DateTime.Now;
            }
        }
    }
}
