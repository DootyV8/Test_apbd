using Test.Models;

namespace TestGroupB.Services;

public interface IDbService
{
    Task<VisitDTO?> GetVisit(int visitId);
    Task CreateVisit(NewVisitDTO newVisit);
}