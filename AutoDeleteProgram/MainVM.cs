using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows;
using Microsoft.Win32;
using System.Data;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AutoDeleteProgram
{
    public class LogData
    {
        public DateTime TimeStamp { get; set; }
        public string Log { get; set; }
        public SolidColorBrush CellColor { get; set; }
    }

    public static class DispatcherService
    {
        public static void Invoke(Action action)
        {
            Dispatcher dispatchObject = System.Windows.Application.Current != null ? System.Windows.Application.Current.Dispatcher : null;
            if (dispatchObject == null || dispatchObject.CheckAccess())
                action();
            else
                dispatchObject.Invoke(action);
        }
    }

    public class MainVM : ObservableObject
    {
        public MainWindow mainWindow;
        private BackgroundWorker deleteByPeriodWorker;
        private BackgroundWorker deleteByDaysWorker;
        public BackgroundWorker DeleteByPeriodWorker
        {
            get => deleteByPeriodWorker;
            set => SetProperty(ref deleteByPeriodWorker, value);
        }
        public BackgroundWorker DeleteByDaysWorker
        {
            get => deleteByDaysWorker;
            set => SetProperty(ref deleteByDaysWorker, value);
        }

        public MainVM(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            DeleteByPeriodWorker = new BackgroundWorker();
            DeleteByPeriodWorker.DoWork += DeleteByPeriod_DoWork;
            DeleteByPeriodWorker.RunWorkerCompleted += DeleteByPeriod_DoWork_RunWorkerCompleted;
            DeleteByPeriodWorker.WorkerReportsProgress = true;
            
            DeleteByDaysWorker = new BackgroundWorker();
            DeleteByDaysWorker.DoWork += DeleteByDays_DoWork;
            DeleteByDaysWorker.RunWorkerCompleted += DeleteByDays_DoWork_RunWorkerCompleted;
            DeleteByDaysWorker.WorkerReportsProgress = true;
        }
        private void DeleteByPeriod_DoWork(object sender, DoWorkEventArgs e)
        {
            if (DirectoryPath != null)
            {
                DateTime selectedTime = new DateTime(SelectedDateTo.Year, SelectedDateTo.Month, SelectedDateTo.Day, 23, 59, 59);
                SelectedDateTo = selectedTime;

                Thread.Sleep(100);
                DirectoryInfo directoryInfo = new DirectoryInfo(DirectoryPath);
                Parallel.ForEach(directoryInfo.GetFiles(), file =>
                {
                    string errorInfo = "";
                    string filePath = file.FullName;
                    bool isDeleted = true;
                    if (file.LastWriteTime >= SelectedDateFrom && file.LastWriteTime <= SelectedDateTo)
                    {
                        try
                        {
                            file.Delete();
                            if (File.Exists(filePath))
                            {
                                isDeleted = false;
                            }
                        }
                        catch(Exception error)
                        {
                            errorInfo = error.ToString();
                        }

                        DispatcherService.Invoke((System.Action)(() =>
                        {
                            DeletionLog.Add(CreateLogData(filePath, isDeleted, errorInfo));
                        }));                
                    }
                });           
                Parallel.ForEach(directoryInfo.GetDirectories(), subDirectory =>
                {
                    string errorInfo = "";
                    string subDirectoryPath = subDirectory.FullName;
                    bool isDeleted = true;

                    if (subDirectory.LastWriteTime >= SelectedDateFrom && subDirectory.LastWriteTime <= SelectedDateTo)
                    {
                        try
                        {
                            subDirectory.Delete(true);
                            if (File.Exists(subDirectoryPath))
                            {
                                isDeleted = false;
                            }
                        }
                        catch (Exception error)
                        {
                            errorInfo = error.ToString();
                        }
                        DispatcherService.Invoke((System.Action)(() =>
                        {
                            DeletionLog.Add(CreateLogData(subDirectoryPath, isDeleted, errorInfo));
                        }));
                    }
                });     
            }
        }
        private void DeleteByPeriod_DoWork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //DeleteByPeriodWorker.DoWork -= DeleteByPeriod_DoWork;
            //DeleteByPeriodWorker.RunWorkerCompleted -= DeleteByPeriod_DoWork_RunWorkerCompleted;
        }
        private void DeleteByDays_DoWork(object sender, DoWorkEventArgs e)
        {
            DeletionActiveFlag = !DeletionActiveFlag;
            if (DirectoryPath != null)
            {
                Thread.Sleep(100);
                DirectoryInfo directoryInfo = new DirectoryInfo(DirectoryPath);
                Parallel.ForEach (directoryInfo.GetFiles(), file => 
                {           
                    if (DeletionActiveFlag == false)
                    {
                        return;
                    }
                     
                    string errorInfo = "";
                    string filePath = file.FullName;
                    bool isDeleted = true;

                    if (file.LastWriteTime > DateTime.Today.AddDays(-1 * DeletionDateRange))
                    {
                        try
                        {
                            file.Delete();
                            if (File.Exists(filePath))
                            {
                                isDeleted = false;
                            }
                        }
                        catch(Exception error)
                        {
                            errorInfo = error.ToString();
                        }

                        DispatcherService.Invoke((System.Action)(() =>
                        {
                            DeletionLog.Add(CreateLogData(filePath, isDeleted, errorInfo));
                        }));
                      
                    }
                });
                Parallel.ForEach(directoryInfo.GetDirectories(), subDirectory =>
                {
                    if (DeletionActiveFlag == false)
                    {
                        return;
                    }
                     
                    string errorInfo = "";
                    string subDirectoryPath = subDirectory.FullName;
                    bool isDeleted = true;

                    if (subDirectory.LastWriteTime > DateTime.Today.AddDays(-1 * DeletionDateRange))
                    {                     
                        try
                        {
                            subDirectory.Delete(true);
                            if (File.Exists(subDirectoryPath))
                            {
                                isDeleted = false;
                            }
                        }
                        catch(Exception error)
                        {
                            errorInfo = error.ToString();
                        }

                        DispatcherService.Invoke((System.Action)(() =>
                        {
                            DeletionLog.Add(CreateLogData(subDirectoryPath, isDeleted, errorInfo));
                        })); 
                    }
                });

                DeletionActiveFlag = false;
            }
        }
        private void DeleteByDays_DoWork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //DeleteByPeriodWorker.DoWork -= DeleteByPeriod_DoWork;
            //DeleteByPeriodWorker.RunWorkerCompleted -= DeleteByPeriod_DoWork_RunWorkerCompleted;     
        }

        string directoryPath;
        public string DirectoryPath
        {
            get => directoryPath;
            set => SetProperty(ref directoryPath, value);
        }

        private DateTime selectedDateFrom = DateTime.Now.AddDays(-7);
        public DateTime SelectedDateFrom
        {
            get => selectedDateFrom;
            set => SetProperty(ref selectedDateFrom, value);
        }

        private DateTime selectedDateTo = DateTime.Now;
        public DateTime SelectedDateTo
        {
            get => selectedDateTo;
            set => SetProperty(ref selectedDateTo, value);
        }

        private int deletionDateRange = 7;
        public int DeletionDateRange
        {
            get => deletionDateRange;
            set => SetProperty(ref deletionDateRange, value);
        }

        private bool deletionActiveFlag = false;
        public bool DeletionActiveFlag
        {
            get => deletionActiveFlag;
            set
            {
                SetProperty(ref deletionActiveFlag, value);
            }
        }

        ObservableCollection<LogData> deletionLog = null;
        public ObservableCollection<LogData> DeletionLog
        {
            get
            {
                if(deletionLog == null)
                {
                    deletionLog = new ObservableCollection<LogData>();
                }
                return deletionLog;
            }
            set
            {
                deletionLog = value;
            }
        }

        public ICommand SelectDirectory
        {
            get => new RelayCommand(
                () =>
                {
                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        DirectoryPath = fbd.SelectedPath;
                    }
                });
        }
        public LogData CreateLogData(string fileName, bool isDeleted, string errorInfo)
        {
            LogData logData = new LogData();
            logData.TimeStamp = DateTime.Now;
            if (isDeleted)
            {
                logData.Log = fileName;
                logData.CellColor = new SolidColorBrush(Color.FromRgb(0, 0, 0));
            }
            else
            {
                logData.Log = "Fail : " + errorInfo + ", Occurred from : " + fileName;
                logData.CellColor = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            }
            //가장 첫 인자를 csv파일 경로로 사용
            AppendLogFile(@"C:\Users\User\Desktop\logtest.txt", logData.Log, logData.TimeStamp);
        
            return logData;
        }
        public void AppendLogFile(string logFilePath, string LogInfo, DateTime timeStamp)
        {
            string lineData = timeStamp + "\t" + LogInfo + "\t" + Environment.NewLine;         
            File.AppendAllText(logFilePath, lineData);
        }
        public ICommand DeleteFilesByPeriod
        {
            get => new RelayCommand(
                () =>
                {
                    if(!DeleteByPeriodWorker.IsBusy)
                        DeleteByPeriodWorker.RunWorkerAsync();    
                    //Thread threadDeleteByPeriod = new Thread(DeleteByPeriod);
                    //threadDeleteByPeriod.Start();
                });
        }
        public ICommand DeleteFilesByDays
        {
            get => new RelayCommand(
                () =>
                {
                    //DeleteByPeriod();
                    if(!DeleteByDaysWorker.IsBusy)
                        DeleteByDaysWorker.RunWorkerAsync();
                    else
                    {
                        if (DeletionActiveFlag)
                            DeletionActiveFlag = false;
                        else
                            DeletionActiveFlag = true;
                    }
                    //Thread threadDeleteByDays = new Thread(DeleteByDays);
                    //threadDeleteByDays.IsBackground = true;
                    //threadDeleteByDays.Start();           
                });
        }
    }
}
