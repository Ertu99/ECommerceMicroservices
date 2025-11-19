using Dapper;
using OrderService.Application.Interfaces;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Database.Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderService.Infrastructure.Database.Repositories
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
                VALUES (@EventType, @Payload, @CreatedAt, @Status);";

            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sql, message);
        }
    }
}
