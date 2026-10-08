using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper.Configuration.Attributes;

namespace Attendix.Models;

public partial class Student : ObservableObject
{
    [Name("Id")] 
    [ObservableProperty] 
    public partial int Id { get; set; }

    [Name("FullName")]
    [ObservableProperty]
    public partial string FullName { get; set; } = null!;

    [Name("EntryDateTime")]
    [ObservableProperty]
    public partial DateTime DateTime { get; set; }
}