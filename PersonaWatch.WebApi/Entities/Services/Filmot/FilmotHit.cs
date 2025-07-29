namespace PersonaWatch.WebApi.Entities.Services.Filmot
{
    public class FilmotHit
    {
        public double Start { get; set; }
        public double Dur { get; set; }
        public string? Token { get; set; }
        public string? CtxBefore { get; set; }
        public string? CtxAfter { get; set; }
    }

}
