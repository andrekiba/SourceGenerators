# C# Source Generators

Here the code related to the sessions I held at **Rome .NET Conference 2020** and **CodeGen 2021**.  

https://youtu.be/HDHlcuNdMwc   
https://youtu.be/K5a5-JlRthc

The most intereseting example is the one about the `DataSource`.  
We substitute the creation of POCO objects, used to read data from the database with Dapper,  
from the use of reflection at runtime to the compile time generation.

To do this the steps are: 
1. search candidate classes marked with the `DataSource` attribute
2. for each generate the `MMetadata` and the list of related `FMetadata`
3. generate the `ModelService` using a [Scriban](https://github.com/scriban/scriban) template
