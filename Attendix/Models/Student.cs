using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Attendix.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper.Configuration.Attributes;

namespace Attendix.Models;

public partial class Student : ObservableObject
{
    // Дата в CSV — фикс. формат dd.MM.yyyy (старые файлы с M/d/yyyy тоже читаются): см. CsvDateOnlyConverter
    [Name("Дата")]
    [TypeConverter(typeof(CsvDateOnlyConverter))]
    [ObservableProperty]
    public partial DateOnly Date { get; set; }

    [Name("Класс")] [ObservableProperty] public partial string Group { get; set; } = null!;

    [Name("Учащийся")]
    [ObservableProperty]
    public partial string FullName { get; set; } = null!;

    [NotMapped]
    [Ignore]
    public string Attendance
    {
        get
        {
            var result = new List<string>();

            if (FirstArrival == TimeOnly.MinValue && LastDeparture == TimeOnly.MinValue)
            {
                result.Add("Прогул");
            }
            else
            {
                if (FirstArrival == TimeOnly.MinValue)
                {
                    result.Add("Отсутствует приход");
                }
                else
                {
                    result.Add(FirstArrival >= Settings.Current.StartOfClasses ? "Поздний приход" : "Приход вовремя");
                }

                if (LastDeparture == TimeOnly.MinValue)
                {
                    result.Add("Отсутствует уход");
                }
                else
                {
                    result.Add(LastDeparture < Settings.Current.EndOfClasses ? "Ранний уход" : "Уход вовремя");
                }
            }

            return string.Join("; ", result);
        }
    }

    [Name("Первый приход")]
    [NotifyPropertyChangedFor(nameof(Attendance), nameof(IsLateArrival), nameof(IsAbsent), nameof(FirstArrivalDisplay))]
    [ObservableProperty]
    public partial TimeOnly FirstArrival { get; set; }

    [Name("Последний уход")]
    [NotifyPropertyChangedFor(nameof(Attendance), nameof(IsEarlyLeave), nameof(IsAbsent), nameof(LastDepartureDisplay))]
    [ObservableProperty]
    public partial TimeOnly LastDeparture { get; set; }

    [NotMapped]
    [Ignore]
    public string FirstArrivalDisplay => FirstArrival == TimeOnly.MinValue ? "(нет)" : FirstArrival.ToString("HH:mm");

    [NotMapped]
    [Ignore]
    public string LastDepartureDisplay =>
        LastDeparture == TimeOnly.MinValue ? "(нет)" : LastDeparture.ToString("HH:mm");

    [NotMapped]
    [Ignore]
    public bool IsAbsent => FirstArrival == TimeOnly.MinValue && LastDeparture == TimeOnly.MinValue;

    [NotMapped]
    [Ignore]
    public bool IsLateArrival => FirstArrival != TimeOnly.MinValue && FirstArrival >= Settings.Current.StartOfClasses;

    [NotMapped]
    [Ignore]
    public bool IsEarlyLeave => LastDeparture != TimeOnly.MinValue && LastDeparture < Settings.Current.EndOfClasses;

    [NotMapped]
    [Ignore]
    public bool LateHighlight => IsLateArrival && Settings.Current.HighlightCells;
    [NotMapped]
    [Ignore]
    public bool EarlyHighlight => IsEarlyLeave && Settings.Current.HighlightCells;
    [NotMapped]
    [Ignore]
    public bool AbsentHighlight => IsAbsent && Settings.Current.HighlightCells;

    public void RefreshAttendance()
    {
        OnPropertyChanged(nameof(Attendance));
        OnPropertyChanged(nameof(IsLateArrival));
        OnPropertyChanged(nameof(IsEarlyLeave));
        OnPropertyChanged(nameof(IsAbsent));
        OnPropertyChanged(nameof(LateHighlight));
        OnPropertyChanged(nameof(EarlyHighlight));
        OnPropertyChanged(nameof(AbsentHighlight));
    }
}