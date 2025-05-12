using Test.Models;
using Test.Exceptions;
using Microsoft.Data.SqlClient;
using Test.Services;
using TestGroupB.Services;


namespace Test.Services;

public class DbService : IDbService
{
    private readonly string _connectionString;

    public DbService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new ArgumentNullException("Connection string not found");
    }

    public async Task<VisitDTO?> GetVisit(int visitId)
    {
        const string query = @"
            SELECT v.date, 
                   c.first_name, c.last_name, c.date_of_birth,
                   m.mechanic_id, m.licence_number,
                   s.name, vs.service_fee
            FROM Visit v
            JOIN Client c ON v.client_id = c.client_id
            JOIN Mechanic m ON v.mechanic_id = m.mechanic_id
            JOIN Visit_Service vs ON vs.visit_id = v.visit_id
            JOIN Service s ON s.service_id = vs.service_id
            WHERE v.visit_id = @visitId";

        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@visitId", visitId);

        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        VisitDTO? visit = null;

        while (await reader.ReadAsync())
        {
            if (visit == null)
            {
                visit = new VisitDTO
                {
                    date = reader.GetDateTime(0),
                    client = new ClientDTO
                    {
                        firstName = reader.GetString(1),
                        lastName = reader.GetString(2),
                        dateOfBirth = reader.GetDateTime(3)
                    },
                    mechanic = new MechanicDTO
                    {
                        mechanicId = reader.GetInt32(4),
                        licenceNumber = reader.GetString(5)
                    },
                    visitServices = new List<VisitServiceDTO>()
                };
            }

            visit.visitServices.Add(new VisitServiceDTO
            {
                name = reader.GetString(6),
                serviceFee = reader.GetDecimal(7)
            });
        }

        if (visit == null)
            throw new NotFoundException($"Visit with ID {visitId} not found.");

        return visit;
    }

    public async Task CreateVisit(NewVisitDTO newVisit)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var command = connection.CreateCommand();
        command.Transaction = (SqlTransaction)transaction;

        try
        {
            command.CommandText = "SELECT 1 FROM Visit WHERE visit_id = @visitId";
            command.Parameters.AddWithValue("@visitId", newVisit.visitId);
            if (await command.ExecuteScalarAsync() is not null)
                throw new ConflictException("Visit already exists with the provided ID");

            command.Parameters.Clear();
            command.CommandText = "SELECT 1 FROM Client WHERE client_id = @clientId";
            command.Parameters.AddWithValue("@clientId", newVisit.clientId);
            if (await command.ExecuteScalarAsync() is null)
                throw new NotFoundException("Client does not exists with the provided ID");

            command.Parameters.Clear();
            command.CommandText = "SELECT mechanic_id FROM Mechanic WHERE licence_number = @licence";
            command.Parameters.AddWithValue("@licence", newVisit.mechanicLicenceNumber);
            var mechanicIdObj = await command.ExecuteScalarAsync();
            if (mechanicIdObj is null)
                throw new NotFoundException("Mechanic does not exists with the provided ID");
            var mechanicId = (int)mechanicIdObj;

            command.Parameters.Clear();
            command.CommandText = @"
                INSERT INTO Visit (visit_id, client_id, mechanic_id, date)
                VALUES (@visitId, @clientId, @mechanicId, GETDATE())";
            command.Parameters.AddWithValue("@visitId", newVisit.visitId);
            command.Parameters.AddWithValue("@clientId", newVisit.clientId);
            command.Parameters.AddWithValue("@mechanicId", mechanicId);
            await command.ExecuteNonQueryAsync();

            foreach (var service in newVisit.services)
            {
                command.Parameters.Clear();
                command.CommandText = "SELECT service_id FROM Service WHERE name = @name";
                command.Parameters.AddWithValue("@name", service.serviceName);
                var serviceIdObj = await command.ExecuteScalarAsync();
                if (serviceIdObj is null)
                    throw new NotFoundException($"Service '{service.serviceName}' not found.");
                var serviceId = (int)serviceIdObj;

                command.Parameters.Clear();
                command.CommandText = @"
                    INSERT INTO Visit_Service (visit_id, service_id, service_fee)
                    VALUES (@visitId, @serviceId, @fee)";
                command.Parameters.AddWithValue("@visitId", newVisit.visitId);
                command.Parameters.AddWithValue("@serviceId", serviceId);
                command.Parameters.AddWithValue("@fee", service.serviceFee);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
