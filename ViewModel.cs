using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WpfProgressbar
{
    public class ViewModel : ViewModelBase
    {
        private double _progress = 25;
        public double Progress
        {
            get { return _progress; }
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        private ProgressState _progressState = ProgressState.None;
        public ProgressState ProgressState
        {
            get { return _progressState; }
            set
            {
                SetProperty(ref _progressState, value);
            }
        }

        private string _progressLabel = "Sample progress bar";
        public string ProgressLabel
        {
            get { return _progressLabel; }
            set { SetProperty(ref _progressLabel, value); }
        }

        public ViewModel() { }
    }


    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (object.Equals(storage, value)) return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
