using System.Globalization;

namespace Fiadex;

/// <summary>
/// Filtro compartido para búsquedas de personas por nombre o teléfono.
/// La comparación ignora mayúsculas y tildes: "maria" encuentra "María".
/// </summary>
public static class PersonSearchFilter
{
    private static readonly CompareInfo Comparer = CultureInfo.InvariantCulture.CompareInfo;

    public static IEnumerable<Person> Filter(IEnumerable<Person> people, string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return people;
        }

        var needle = term.Trim();
        return people.Where(person =>
            Matches(person.Name, needle) || Matches(person.Phone, needle));
    }

    private static bool Matches(string? value, string needle) =>
        !string.IsNullOrEmpty(value) &&
        Comparer.IndexOf(value, needle, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
