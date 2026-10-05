using Microsoft.Data.SqlClient;

namespace BoLagom.Api.Models;

internal enum SqlServerErrorCode
{
    ConstraintViolation = 547,
    DuplicateKey = 2601,
    UniqueConstraintViolation = 2627
}

internal static class SqlServerErrorExtensions
{
    public static bool IsDuplicateApartmentNumber(this SqlException exception)
    {
        return ((SqlServerErrorCode)exception.Number is
            SqlServerErrorCode.DuplicateKey or SqlServerErrorCode.UniqueConstraintViolation) &&
            exception.Message.Contains("UQ_ApartmentNumber_DiffProperty", StringComparison.Ordinal);
    }

    public static bool IsApartmentPropertyForeignKeyViolation(this SqlException exception)
    {
        return exception.Number == (int)SqlServerErrorCode.ConstraintViolation &&
            exception.Message.Contains("FK_Apartments_Properties", StringComparison.Ordinal);
    }
}
