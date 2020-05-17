using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tests
{
    public class Tests
    {
        private readonly Mock<DatabaseContext> _contextMock;
        private readonly IMapper autoMapper;

        public Tests()
        {
            _contextMock = new Mock<DatabaseContext>(options);

            autoMapper = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new MappingProfile());
                mc.AddProfile(new UTMappingProfile());

            }).CreateMapper();
        }

        [Fact]
        public async Task TestOne()
        {
            //arrange
            var mockedEntityList = new List<RandomEntityObject>
            {
                new RandomEntityObject()
                {
                    ValueOne = 1,
                    ValueTwo = 1
                }
            };


            _contextMock.PrepareMockContext(arrange);

            var repository = new RandomRepository(_contextMock.Object, autoMapper);

            //act
            var callRepository = await repository.RandomMethod(1);

            //assert
            Assert.True(callRepository);
        }
    }
}