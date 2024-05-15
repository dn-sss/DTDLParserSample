# DTDLParserSample using Co-Types


## Co-Types

DTDL allows to add arbitrrary, undefined element types as co-types.  When an element has an co-type, undefined properties with arbitrary values can be added to the element.

Please refer to https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/DTDL.v2.md#undefined-co-types-and-properties for more details on co-types.

### Example

- Co-Types : MinMax
- Property = Value : maxValue = 10, minValue = 1

```json
{
    "@type": [
        "Property",
        "MinMax"
    ],
    "displayName": {
        "en": "Simple Property with MinMax Type"
    },
    "name": "simplePropertyWithMinMax",
    "schema": "integer",
    "maxValue": 10,
    "minValue": 1,
    "writable": true
}
```

## DTDL v3 Language Extension & Co-type

DTDL v3 supports language extensions (https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v3/DTDL.Extensions.md).  With DTDL v3, we can use co-types under Language Extension concept.

## How to extend DTDL with co-types.

1. (DTDL v3 only) Add DTDL ID for the extension's context specifier  
    &nbsp; 
    e.g. add **`dtmi:com:company_foo:extension:dummy;1`** to **`@context`**
    &nbsp;  
    ```json
    {
    "@context": [ "dtmi:dtdl:context;3", "dtmi:com:company_foo:extension:dummy;1"` ],
    "@id": "dtmi:com:company_foo:sample_base_model;1",
    "@type": "Interface",
        :
        :
    ```

1. Add a co-type(s) to elements, then define property and value
    &nbsp;
    Example 1: Simple Integer Property with co-type **`MinMax`**
    &nbsp;
    ```json
    {
        "@type": [
            "Property",
            "MinMax"
        ],
        "name": "PropertyWithMainMax",
        "schema": "integer",
        "maxValue": 10,
        "minValue": 1,
    }
    ```
    &nbsp;
    Example 2 : JSON Object type with co-typed fields
    &nbsp;
    ```json
    {
        "@type": "Property",
        "name": "ObjectWithMinMaxInField",
        "schema": {
            "@type": "Object",
            "fields": [
                {
                    "@type": [ "Field", "MinMax" ],
                    "name": "Field1_Integer",
                    "schema": "integer",
                    "minValue": 0,
                    "maxValue": 10
                },
                {
                    "@type": [ "Field", "MinMax" ],
                    "name": "Field2_Double",
                    "schema": "double",
                    "minValue": 0.1,
                    "maxValue": 9.9
                }
            ]
        }
    }
    ```

1. Parse DTDL

    >
    > [!IMPORTANT]  
    > For DTDL v3, ParsingOption **AllowUndefinedExtensions** must be set to Always
    >

    ```csharp
    using DTDLParser;  

        :
        :
    
    ModelParser parser = new ModelParser(
        new ParsingOptions()
        {
            AllowUndefinedExtensions = WhenToAllow.Always
        }
    );
    ```

1. Co-types and properties and values will be parsed to **`UndefinedTypes`** and **`UndefinedProperties`**

    ```text
    - DtEntity                  : {DTDLParser.Models.DTPropertyInfo}
      +	ChildOf	                : {dtmi:com:company_foo:sample_base_model;1}
      +	ClassId	                : {dtmi:dtdl:class:Property;3}
        Comment	                : null
      +	DefinedIn               : {dtmi:com:company_foo:sample_base_model;1}
      +	Description	            : Count = 0
      +	DisplayName	            : Count = 1
        EntityKind	            : Property
      +	Id	                    : {dtmi:com:company_foo:sample_base_model:_contents:__simplePropertyWithMinMax;1}
        LanguageMajorVersion    : 3
        Name	                : "simplePropertyWithMinMax"
      +	Schema	                : {DTDLParser.Models.DTIntegerInfo}
      +	SupplementalProperties  : Count = 0
      +	SupplementalTypes	    : Count = 0
      -	UndefinedProperties	    : Count = 2	
        - [0]	                : {[maxValue, ValueKind = Number : "10"]}	
            Key	                : "maxValue"
            Value	            : ValueKind = Number : "10"
        - [1]                   : {[minValue, ValueKind = Number : "1"]}
            Key	                : "minValue"
            Value	            : ValueKind = Number : "1"
      -	UndefinedTypes	        : Count = 1
        - [0]                   : "MinMax"
    ```