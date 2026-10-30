namespace ElApostol_WPF.Models
{
    public class Categoria
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public int CantidadProductos { get; set; }

        /// <summary>Prefijo de código: primeras 3 letras del nombre sin acentos (Construcción → CON).</summary>
        public string Prefijo => CalcularPrefijo(Nombre);

        public static string CalcularPrefijo(string nombre)
        {
            var sinAcentos = new string(nombre
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                            != System.Globalization.UnicodeCategory.NonSpacingMark)
                .Where(char.IsLetter)
                .ToArray());

            return sinAcentos[..Math.Min(3, sinAcentos.Length)].ToUpperInvariant();
        }
    }
}
