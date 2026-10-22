namespace AppNutricion.Api;

public static class FrontendSecurity
{
    // Solo recursos del mismo origen. Sin 'unsafe-inline': ni scripts ni estilos inline.
    // img-src permite data: y blob: (íconos y la descarga del export); connect-src 'self' es la API.
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
}
