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
            DeleteByPeriodWorker.ProgressChanged += DeletionProgressChanged;
      
            DeleteByDaysWorker = new BackgroundWorker();
            DeleteByDaysWorker.DoWork += DeleteByDays_DoWork;
            DeleteByDaysWorker.RunWorkerCompleted += DeleteByDays_DoWork_RunWorkerCompleted;
            DeleteByDaysWorker.WorkerReportsProgress = true;
            DeleteByDaysWorker.ProgressChanged += DeletionProgressChanged;
        }
        private void DeleteFilesProcess(FileInfo file ,string filePath, ref int finishedFilesCount, ref int totalFilesCount)
        {
            string errorInfo = "";
            bool isDeleted = true;
            try
            {
                file.Delete();
                if (File.Exists(filePath))
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
                DeletionLog.Add(CreateLogData(filePath, isDeleted, errorInfo));
            }));
        }
        private void DeleteDirectoriesProcess(DirectoryInfo subDirectory, string subDirectoryPath, ref int finishedFilesCount, ref int totalFilesCount)
        {
            string errorInfo = "";
            bool isDeleted = true;
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
        private void DeleteByPeriod_DoWork(object sender, DoWorkEventArgs e)
        {
            if (DeleteDirectoryPath == null)
                return;
            
            DateTime selectedTime = new DateTime(SelectedDateTo.Year, SelectedDateTo.Month, SelectedDateTo.Day, 23, 59, 59);
            SelectedDateTo = selectedTime;

            DirectoryInfo directoryInfo = new DirectoryInfo(DeleteDirectoryPath);
            int totalFilesCount = 0;
            int finishedFilesCount = 0;

            TimeSpan timeSpan = new TimeSpan(0, 0, 0);
            FileInfo[] files = directoryInfo.GetFiles();
            DirectoryInfo[] subDirectories = directoryInfo.GetDirectories();
            //해당 범위 안에 생성된 디렉토리와 파일 갯수를 확인
            totalFilesCount += Array.FindAll(files, file => DateTime.Compare(file.LastWriteTime + timeSpan, selectedDateTo + timeSpan) <= 0 
            && DateTime.Compare(file.LastWriteTime + timeSpan, selectedDateFrom + timeSpan) >= 0).Length;
            totalFilesCount += Array.FindAll(subDirectories, dir => DateTime.Compare(dir.LastWriteTime + timeSpan, selectedDateTo + timeSpan) <= 0 
            && DateTime.Compare(dir.LastWriteTime + timeSpan, selectedDateFrom + timeSpan) >= 0).Length;

            DateTime rangeLowerBound = new DateTime(SelectedDateFrom.Year, selectedDateFrom.Month, selectedDateFrom.Day, 0, 0, 0);
            DateTime rangeUpperBound = new DateTime(SelectedDateTo.Year, selectedDateTo.Month, selectedDateTo.Day, 0, 0, 0);

            Parallel.ForEach(files, file =>
            {              
                DateTime lastWriteDay = new DateTime(file.LastWriteTime.Year, file.LastWriteTime.Month, file.LastWriteTime.Day, 0, 0, 0);

                if (!(DateTime.Compare(lastWriteDay, rangeLowerBound) >= 0 && DateTime.Compare(lastWriteDay, rangeUpperBound) <= 0))
                    return;
                    
                DeleteFilesProcess(file, file.FullName, ref finishedFilesCount, ref totalFilesCount);
                DeleteByPeriodWorker.ReportProgress((++finishedFilesCount) * 100 / totalFilesCount);
                Thread.Sleep(50);
            });           
            Parallel.ForEach(subDirectories, subDirectory =>
            {
                DateTime lastWriteDay = new DateTime(subDirectory.LastWriteTime.Year, subDirectory.LastWriteTime.Month, subDirectory.LastWriteTime.Day, 0, 0, 0);

                if (!(DateTime.Compare(lastWriteDay, rangeLowerBound) >= 0 && DateTime.Compare(lastWriteDay, rangeUpperBound) <= 0))
                    return;                           
                       
                DeleteDirectoriesProcess(subDirectory, subDirectory.FullName, ref finishedFilesCount, ref totalFilesCount);
                DeleteByPeriodWorker.ReportProgress((++finishedFilesCount) * 100 / totalFilesCount);
                Thread.Sleep(50);
            });      
        }
        private void DeleteByPeriod_DoWork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {}

        private bool IsDeletionTime(DateTime currentTime)
        {
            if (currentTime.Hour == AutoDeletionHour && currentTime.Minute == AutoDeletionMinutes)/*(currentTime.Hour == 0 && currentTime.Minute == 0)*/
                return true;
            else
                return false;
        }
        private void DeleteByDays_DoWork(object sender, DoWorkEventArgs e)
        {
            DeletionByDaysActiveFlag = !DeletionByDaysActiveFlag;
            while(DeletionByDaysActiveFlag)
            {
                EnableManualButton = false;
                DateTime currentTime = DateTime.Now;

                if (DeleteDirectoryPath == null || !IsDeletionTime(currentTime))
                {
                    Thread.Sleep(10000);//10 seconds
                    continue;
                }
                DirectoryInfo directoryInfo = new DirectoryInfo(DeleteDirectoryPath);
                int totalFilesCount = 0;
                int finishedFilesCount = 0;
                TimeSpan timeSpan = new TimeSpan(0, 0, 0);
                FileInfo[] files = directoryInfo.GetFiles();
                DirectoryInfo[] subDirectories = directoryInfo.GetDirectories();

                totalFilesCount += Array.FindAll(files, file => DateTime.Compare(file.LastWriteTime + timeSpan,
                    (currentTime + timeSpan).AddDays(-1 * DeletionDateRange)) <= 0).Length;
                totalFilesCount += Array.FindAll(subDirectories, dir => DateTime.Compare(dir.LastWriteTime + timeSpan,
                    (currentTime + timeSpan).AddDays(-1 * DeletionDateRange)) <= 0).Length;

                if (totalFilesCount == 0)
                {
                    Thread.Sleep(10000);//10 seconds
                    continue;
                }
                File.AppendAllText(LogDirectoryPath, "삭제 시작 :" + DateTime.Now + " " + Environment.NewLine);

                Parallel.ForEach(files, file =>
                {
                    if (!DeletionByDaysActiveFlag)
                    {
                        return;
                    }
                    DateTime modifiedTime = new DateTime(file.LastWriteTime.Year, file.LastWriteTime.Month, file.LastWriteTime.Day, 0, 0, 0);
                    DateTime compareDate = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 0, 0, 0).AddDays(-1 * DeletionDateRange);

                    if (DateTime.Compare(modifiedTime, compareDate) <= 0 && DeletionByDaysActiveFlag)
                    {
                        DeleteFilesProcess(file, file.FullName, ref finishedFilesCount, ref totalFilesCount);
                        DeleteByDaysWorker.ReportProgress((++finishedFilesCount) * 100 / totalFilesCount);
                    }
                });
                Parallel.ForEach(subDirectories, subDirectory =>
                {
                    if (!DeletionByDaysActiveFlag)
                    {
                        return;
                    }
                    DateTime modifiedTime = new DateTime(subDirectory.LastWriteTime.Year, subDirectory.LastWriteTime.Month, subDirectory.LastWriteTime.Day, 0, 0, 0);
                    DateTime compareDate = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 0, 0, 0).AddDays(-1 * DeletionDateRange);

                    if (DateTime.Compare(modifiedTime, compareDate) <= 0 && DeletionByDaysActiveFlag)
                    {
                        DeleteDirectoriesProcess(subDirectory, subDirectory.FullName, ref finishedFilesCount, ref totalFilesCount);
                        DeleteByDaysWorker.ReportProgress((++finishedFilesCount) * 100 / totalFilesCount);
                    }
                });
                File.AppendAllText(LogDirectoryPath, "삭제 종료 : " + DateTime.Now + " " + Environment.NewLine);
                Thread.Sleep(10000);//10 seconds
            }
            EnableManualButton = true;
        }
        private void DeleteByDays_DoWork_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //DeleteByPeriodWorker.DoWork -= DeleteByPeriod_DoWork;
            //DeleteByPeriodWorker.RunWorkerCompleted -= DeleteByPeriod_DoWork_RunWorkerCompleted;     
        }
        private void DeletionProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressPercent = e.ProgressPercentage;

            if (ProgressPercent == 100)
            {
                ProgressPercent = 0;
            }
        }
        string deleteDirectoryPath;
        public string DeleteDirectoryPath
        {
            get => deleteDirectoryPath;
            set => SetProperty(ref deleteDirectoryPath, value);
        }
        string logDirectoryPath;
        public string LogDirectoryPath
        {
            get => logDirectoryPath;
            set => SetProperty(ref logDirectoryPath, value);
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

        private int deletionDateRange = 1;
        public int DeletionDateRange
        {
            get => deletionDateRange;
            set => SetProperty(ref deletionDateRange, value);
        }

        private bool enableManualButton = true;
        public bool EnableManualButton
        {
            get => enableManualButton;
            set => SetProperty(ref enableManualButton, value);

        }
        private bool deletionByDaysActiveFlag = false;
        public bool DeletionByDaysActiveFlag
        {
            get => deletionByDaysActiveFlag;
            set => SetProperty(ref deletionByDaysActiveFlag, value);
        }

        private int progressPercent = 0;
        public int ProgressPercent
        {
            get => progressPercent;
            set => SetProperty(ref progressPercent, value);
        }

        private int autoDeletionHour = 0;
        public int AutoDeletionHour
        {
            get => autoDeletionHour;
            set {
                if (Convert.ToInt32(value) < 24 && Convert.ToInt32(value) >= 0)
                    SetProperty(ref autoDeletionHour, value);
                else
                    SetProperty(ref autoDeletionHour, 0);
            } 
        }

        private int autoDeletionMinutes = 0;
        public int AutoDeletionMinutes
        {
            get => autoDeletionMinutes;
            set
            {
                if (Convert.ToInt32(value) < 60 && Convert.ToInt32(value) >= 0)
                    SetProperty(ref autoDeletionMinutes, value);
                else
                    SetProperty(ref autoDeletionMinutes, 0);
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
            AppendLogFile(LogDirectoryPath, logData.Log, logData.TimeStamp);
        
            return logData;
        }
        public void AppendLogFile(string logFilePath, string LogInfo, DateTime timeStamp)
        {
            string lineData = timeStamp + "\t" + LogInfo + "\t" + Environment.NewLine;         
            File.AppendAllText(logFilePath, lineData);
        }
        public ICommand SelectDeletionDirectory
        {
            get => new RelayCommand(
                () =>
                {
                    FolderBrowserDialog fbd = new FolderBrowserDialog();
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        DeleteDirectoryPath = fbd.SelectedPath;
                    }
                });
        }
        public ICommand SelectLogDirectory
        {
            get => new RelayCommand(
                () =>
                {
                    System.Windows.Forms.OpenFileDialog fbd = new System.Windows.Forms.OpenFileDialog();
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        LogDirectoryPath = fbd.FileName;
                    }
                });
        }
        public ICommand DeleteByPeriod
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
        public ICommand DeleteByDays
        {
            get => new RelayCommand(
                () =>
                {
                    //DeleteByPeriod();
                    if(!DeleteByDaysWorker.IsBusy)
                        DeleteByDaysWorker.RunWorkerAsync();
                    else
                    {
                        if (DeletionByDaysActiveFlag)
                            DeletionByDaysActiveFlag = false;
                        else
                            DeletionByDaysActiveFlag = true;
                    }
                    //Thread threadDeleteByDays = new Thread(DeleteByDays);
                    //threadDeleteByDays.IsBackground = true;
                    //threadDeleteByDays.Start();           
                });
        }
    }
}
