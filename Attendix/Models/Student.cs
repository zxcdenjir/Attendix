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
    [Name("Дата")] [ObservableProperty] public partial DateOnly Date { get; set; }

    [Name("Класс")] [ObservableProperty] public partial string Group { get; set; } = null!;

    [Name("Учащийся")]
    [ObservableProperty]
    public partial string FullName { get; set; } = null!;

    [NotMapped]
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
                    result.Add(FirstArrival >= Settings.StartOfСlasses ? "Поздний приход" : "Приход вовремя");
                }
                
                if (LastDeparture == TimeOnly.MinValue)
                {
                    result.Add("Отсутствует уход");
                }
                else
                {
                    result.Add(LastDeparture < Settings.EndOfСlasses ? "Ранний уход" : "Уход вовремя");
                }
            }
            
            return  string.Join("; ", result);
        }
    }

    [Name("Первый приход")]
    [NotifyPropertyChangedFor(nameof(Attendance))]
    [ObservableProperty]
    public partial TimeOnly FirstArrival { get; set; }

    [Name("Последний уход")]
    [NotifyPropertyChangedFor(nameof(Attendance))]
    [ObservableProperty]
    public partial TimeOnly LastDeparture { get; set; }
}