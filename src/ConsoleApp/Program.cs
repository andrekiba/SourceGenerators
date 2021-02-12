using DataSource;

namespace ConsoleApp
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            //HelloWorld
            //HelloWorldGenerated.HelloWorld.SayHello();
            
            //DI
            //var foo = DI.ServiceLocator.GetService<IFoo>();
            //var bar = DI.ServiceLocator.GetService<IBar>();

            //Enum
            /*
            TestSimple(Simple.First);
            TestComplex(Complex.Fifth);
            TestComplex((Complex) 6);
            
            static void TestSimple(Simple simple)
            {
                EnumValidation.EnumValidator.Validate(simple);
            }
            
            static void TestComplex(Complex complex)
            {
                EnumValidation.EnumValidator.Validate(complex);
            }
            */

            //DataSource
            var test = new Test();
            var mTest = ModelService.GetMetadata<Test>();
            var user = new User();
            var mUser = ModelService.GetMetadata<User>();
        }
    }
    
    #region DI
    
    [DI.Transient]
    internal interface IFoo
    {
    }

    internal class Foo : IFoo
    {
    }
    
    internal interface IBar
    {
    }

    internal class Bar : IBar
    {
    }

    #endregion
    
    #region Enum

    internal enum Simple
    {
        First,
        Second
    }

    internal enum Complex
    {
        First = 3,
        Second = 4,
        //Sixth = 6,
        Third = 7,
        Fourth = 8,
        Fifth = 9
    }
    
    #endregion 
    
    #region DataSource

    [DataSource("tests_tb")]
    public class Test
    {
        [Column]
        public string Target { get; set; }
        
        [Column("runtime")]
        public string Runtime { get; set; }
    }
    
    [DataSource("logs_tb")]
    public class Log
    {
        [Column]
        public string Error { get; set; }
    }
    
    [DataSource("users_vw", DataSourceType.FileQuery)]
    public class User
    {
        [Column("mail")]
        public string Email { get; set; }
        
        [Column]
        public string FirstName { get; set; }
        
        [Column]
        public string LastName { get; set; }
    }

    #endregion 
}