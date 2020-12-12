using System;
using System.Collections.Generic;
using DataSource;

namespace ConsoleApp
{
    #region HelloWorld
    
    /*
    internal static class Program
    {
        static void Main(string[] args)
        {
            HelloWorldGenerated.HelloWorld.SayHello();
        }
    }
    */
    
    #endregion
    
    #region DI
    
    /*
    internal static class Program
    {
        static void Main(string[] args)
        {
            //var foo = DI.ServiceLocator.GetService<IFoo>();
            Console.ReadLine();
        }
    }
    
    [DI.Transient]
    internal interface IFoo
    {
    }

    internal class Foo : IFoo
    {
    }
    */
    
    #endregion
    
    #region DataSource
    
    internal static class Program
    {
        static void Main(string[] args)
        {
            var test = new Test();
            
            var mTest = ModelService.GetMetadata<Test>();
        }
    }
    
    [DataSource("tests_tb")]
    public class Test
    {
        [Column]
        public string Target { get; set; }
        
        [Column("targettone")]
        public string Target1 { get; set; }
    }
    
    [DataSource("logs_tb")]
    public class Log
    {
        [Column]
        public string Error { get; set; }
    }
    
    [DataSource("users_tb")]
    public class User
    {
        [Column]
        public string Name { get; set; }
    }
    
    #endregion 
}