using MahApps.Metro.IconPacks;
using System.Globalization;
using System.Windows.Data;

namespace GUI.Converters;

public class BoolToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isDirectory && isDirectory)
        {
            return PackIconMaterialKind.Folder; // Folder Icon
        }
        return PackIconMaterialKind.FileOutline; // File Icon
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}