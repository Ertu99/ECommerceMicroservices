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
    public class OutboxRepository : IOutboxRepository
    {
        private readonly DapperContext _context;

        public OutboxRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task AddAsync(OutboxMessage message)
        {
            var sql = @"
                INSERT INTO OutboxMessages (EventType, Payload, CreatedAt, Status)
                VALUES (@EventType, @Payload, @CreatedAt, @Status);
            ";

            using var conn = _context.CreateConnection();
            await conn.ExecuteAsync(sql, message);
        }
    }
}
