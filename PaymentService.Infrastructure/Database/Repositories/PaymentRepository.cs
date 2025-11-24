using Dapper;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Database.Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaymentService.Infrastructure.Database.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly DapperContext _context;
        public PaymentRepository(DapperContext context)
        {
            _context = context;
            
        }
        public async Task<int> AddAsync(Payment payment)
        {
            var sql = @"
                INSERT INTO Payments (OrderId , Amount , Status, CreatedAt)
                VALUES (@OrderId, @Amount, @Status , @CreatedAt)
                RETURNING Id;
            ";

            using var conn = _context.CreateConnection();
            return await conn.ExecuteScalarAsync<int>(sql, payment);
        }
    }
}
