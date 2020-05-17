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

namespace Tests.Common
{
    public static class MockHelper
    {
        /// <summary>
        /// Changes a List object to IQuerableObject and then returns it as a Mock of DbSet<T>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mockData"></param>
        /// <returns>Returns Mock<DbSet<T>></returns>
        private static Mock<DbSet<T>> GenerateAsQuerableMock<T>(List<T> mockData) where T : class
        {

            var mockDataQuerable = InitiateteAndConvert(mockData);

            var mockSet = new Mock<DbSet<T>>();

            mockSet.As<IAsyncEnumerable<T>>()
                .Setup(m => m.GetEnumerator())
                .Returns(new TestAsyncEnumerator<T>(mockDataQuerable.GetEnumerator()));

            mockSet.As<IQueryable<T>>()
                .Setup(m => m.Provider)
                .Returns(new TestAsyncQueryProvider<T>(mockDataQuerable.Provider));

            mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(mockDataQuerable.Expression);
            mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(mockDataQuerable.ElementType);
            mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => mockDataQuerable.GetEnumerator());

            return mockSet;
        }

        public static IQueryable<T> InitiateteAndConvert<T>(List<T> mockData) where T : class
        {
            //We need to instantiate all the class objects that the list has
            //Link: https://stackoverflow.com/questions/43240089/why-is-automapper-projectto-throwing-a-null-reference-exception
            foreach (var item in mockData)
            {
                foreach (var property in item.GetType().GetProperties())
                {
                    if (property.PropertyType.IsClass &&
                       property.PropertyType != typeof(string) &&
                       property.GetValue(item, null) == null)
                    {
                        var propertyType = property.PropertyType;
                        var defaultValue = Activator.CreateInstance(propertyType);

                        property.SetValue(item, defaultValue, null);
                    }
                }
            }

            IQueryable<T> mockDataQuerable = mockData.AsQueryable();

            return mockDataQuerable;
        }

        /// <summary>
        /// Extension method to not apply setup for each mockContext and only pass the List of T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mockContext"></param>
        /// <param name="mockData"></param>
        public static void PrepareMockContext<T>(this Mock<EfContext> mockContext, List<T> mockData) where T : class
        {
            mockContext.Setup(c => c.Set<T>()).Returns(GenerateAsQuerableMock(mockData).Object);
        }
    }

    #region Logic to be able to use EntityFramework async objects like FirstOrDefaultAsync, ToListAsync() etc.


    /// <summary>
    /// Link: https://stackoverflow.com/questions/40476233/how-to-mock-an-async-repository-with-entity-framework-core
    /// Link: https://stackoverflow.com/questions/44807618/automapper-unable-to-cast-testdbasyncenumerable-to-iqueryable
    /// Link: https://stackoverflow.com/questions/43240089/why-is-automapper-projectto-throwing-a-null-reference-exception
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;

        internal TestAsyncQueryProvider(IQueryProvider inner)
        {
            _inner = inner;
        }

        public IQueryable CreateQuery(Expression expression)
        {
            switch (expression)
            {
                case MethodCallExpression m:
                    {
                        var resultType = m.Method.ReturnType; // it shoud be IQueryable<T>
                        var tElement = resultType.GetGenericArguments()[0];

                        var queryType = typeof(TestAsyncEnumerable<>).MakeGenericType(tElement);
                        return (IQueryable)Activator.CreateInstance(queryType, expression);
                    }

            }
            return new TestAsyncEnumerable<TEntity>(expression);
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            var queryType = typeof(TestAsyncEnumerable<>).MakeGenericType(typeof(TElement));
            return (IQueryable<TElement>)Activator.CreateInstance(queryType, expression);
        }

        public object Execute(Expression expression)
        {
            return _inner.Execute(expression);
        }

        public TResult Execute<TResult>(Expression expression)
        {
            return _inner.Execute<TResult>(expression);
        }

        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
        {
            return new TestAsyncEnumerable<TResult>(expression);
        }

        public Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            return Task.FromResult(Execute<TResult>(expression));
        }
    }

    internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable)
            : base(enumerable)
        { }

        public TestAsyncEnumerable(Expression expression)
            : base(expression)
        { }

        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
        }

        IQueryProvider IQueryable.Provider
        {
            get { return new TestAsyncQueryProvider<T>(this); }
        }
    }

    internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;

        public TestAsyncEnumerator(IEnumerator<T> inner)
        {
            _inner = inner;
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        public T Current
        {
            get
            {
                return _inner.Current;
            }
        }

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_inner.MoveNext());
        }
    }
    #endregion
}
