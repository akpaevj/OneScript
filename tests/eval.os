﻿Перем юТест;
Перем Глобал;

Функция ПолучитьСписокТестов(ЮнитТестирование) Экспорт
	
	юТест = ЮнитТестирование;
	
	ВсеТесты = Новый Массив;
	
	ВсеТесты.Добавить("ТестДолжен_ПроверитьВычислениеПростогоВыражения");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьВычислениеВызоваФункции");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьВычислениеБроскаИсключения");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьОператорВыполнить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьОбращениеКЛокальнымПеременным");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьОператорВыполнитьСВыбросомИсключения");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьЧтоВОператореВыполнитьЗапрещенВозврат");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьСвойствоЭтотОбъект_issue712");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьКешКомпиляцииВРазныхФреймах");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьРекурсивныйВызовВычислить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьСложныеВложенныеВызовыВычислить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьВычислитьВнутриВыполнить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьАлиасыФункцийИПеременныхВВычислить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьОбработкуИсключенияВВыполнить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьРекурсивныйВызовВыполнить");
	ВсеТесты.Добавить("ТестДолжен_ПроверитьВложенныеВызовыВыполнить");	
	ВсеТесты.Добавить("ТестДолжен_ПроверитьВызовыВыполнитьСПопытками");

	Возврат ВсеТесты;
КонецФункции

Процедура ТестДолжен_ПроверитьВычислениеПростогоВыражения() Экспорт

	юТест.ПроверитьРавенство(4, Вычислить("2 + 2"));
	
	ВнешняяПеременная = 1;
	юТест.ПроверитьРавенство(1, Вычислить("ВнешняяПеременная"));

КонецПроцедуры

Функция НехорошийМетод()
	ВызватьИсключение "ААА";
КонецФункции

Функция ХорошийМетод()
	Возврат Сред("ААА",2,1);
КонецФункции

Процедура ТестДолжен_ПроверитьВычислениеВызоваФункции() Экспорт
	Текст = "";
	Для Сч = 1 По 3 Цикл
		Текст = Текст + Вычислить("ХорошийМетод()");
	КонецЦикла;
	
	юТест.ПроверитьРавенство("ААА", Текст);
	
КонецПроцедуры

Процедура ТестДолжен_ПроверитьВычислениеБроскаИсключения() Экспорт
	
	Перем ОК;
	
	Попытка
		А = Вычислить("НехорошийМетод()");
	Исключение
		ТекстОшибки = ИнформацияОбОшибке().Описание;
		Сообщить("Получено исключение: " + ТекстОшибки);
		ОК = Истина;
	КонецПопытки;
	
	юТест.ПроверитьИстину(ОК, "Проверяем, что после исключения вернулись в тот же кадр стека вызовов");
		
КонецПроцедуры

Процедура ТестДолжен_ПроверитьОператорВыполнить() Экспорт
	
	ВнешнийКонтекст = "Привет";
	
	Выполнить "ВнешнийКонтекст = ""Пока""";
	
	юТест.ПроверитьРавенство("Пока", ВнешнийКонтекст);
	
КонецПроцедуры

Процедура ТестДолжен_ПроверитьОбращениеКЛокальнымПеременным() Экспорт

	Массив = Новый Массив();
	Массив.Добавить(1);
	Массив.Добавить(2);
	Массив.Добавить(3);

	Результат = 0;
	
	КодДляВыполнения = "
	|Для Каждого ОчередноеНечто Из Массив Цикл
	|	Результат = Результат + ОчередноеНечто;
	|КонецЦикла;";

	Выполнить(КодДляВыполнения);
	
	юТест.ПроверитьРавенство(6, Результат);

КонецПроцедуры

Процедура ТестДолжен_ПроверитьОператорВыполнитьСВыбросомИсключения() Экспорт
	
	ВнешнийКонтекст = "Привет";
	
	Попытка
		Выполнить "ВнешнийКонтекст = ""Пока"";
		|ВызватьИсключение 123;";
	Исключение
		юТест.ПроверитьРавенство("Пока", ВнешнийКонтекст);
		юТест.ПроверитьРавенство("123", ИнформацияОбОшибке().Описание);
		Возврат;
	КонецПопытки;
	
	ВызватьИсключение "Должно было быть выдано исключение, но его не было";
	
КонецПроцедуры

Процедура ТестДолжен_ПроверитьЧтоВОператореВыполнитьЗапрещенВозврат() Экспорт
	
	ТекстМетода = "А = 1;
	|Возврат А;";
	
	Попытка
		Выполнить ТекстМетода;
	Исключение
		
		Эталон = НСтр("ru='Оператор ""Возврат"" может использоваться только внутри метода';en='Return operator may not be used outside procedure or function'");
		юТест.ПроверитьИстину(Найти(ИнформацияОбОшибке().Описание, Эталон)>0);
		Возврат;
	КонецПопытки;
	
	ВызватьИсключение "Должно было быть выдано исключение, но его не было";
	
КонецПроцедуры

Процедура ТестДолжен_ПроверитьСвойствоЭтотОбъект_issue712() Экспорт
	
	ПутьСценарий = ОбъединитьПути(ТекущийСценарий().Каталог, "testdata", "thisObjClass.os");
	Сценарий = ЗагрузитьСценарий(ПутьСценарий);
	Сценарий.Идентификатор = "Это класс";
	
	Результат = "";
	КодВыполнения = "Результат = Сценарий.ПолучитьИдентификатор()";
	Выполнить(КодВыполнения);
	юТест.ПроверитьРавенство("Это класс", Результат);
	
КонецПроцедуры

Функция ТестМетод1(П1)
    
    ЛокальнаяПеременная = 1;
    
    Возврат Вычислить("ЛокальнаяПеременная + П1 + 1");
    
КонецФункции

Функция ТестМетод2(П2, П1)
    ДругаяПеременная = 3;
    ЛокальнаяПеременная = 1;
        
    Возврат Вычислить("ЛокальнаяПеременная + П1 + 1");
КонецФункции

Процедура ТестДолжен_ПроверитьКешКомпиляцииВРазныхФреймах() Экспорт
    
    Рез1 = ТестМетод1(1);
    Рез2 = ТестМетод1(1);
    Рез3 = ТестМетод2(Неопределено, 1);
    Рез4 = ТестМетод2(Неопределено, 1);
    
    юТест.ПроверитьРавенство(3, Рез1);
    юТест.ПроверитьРавенство(3, Рез2);
    юТест.ПроверитьРавенство(3, Рез3);
    юТест.ПроверитьРавенство(3, Рез4);
    
КонецПроцедуры

Процедура ТестДолжен_ПроверитьРекурсивныйВызовВычислить() Экспорт
	Рез = "Вычислить(1)";
	Для й=1 По 100 Цикл
	  Рез = "Вычислить("+Рез+")+1";
	КонецЦикла;
	
	Рез = Вычислить(Рез);
    юТест.ПроверитьРавенство(101, Рез);
КонецПроцедуры


Функция Один()
	Возврат Вычислить("1");
КонецФункции

Функция Два()
  Один = Вычислить("Один()"); 
  Возврат Вычислить("Один+Один()");
КонецФункции

Функция Шесть() 
	Три = 3;
	Возврат Вычислить("Два()") * Три;
КонецФункции

Функция СорокДва(Семь)
	Возврат Вычислить("Семь * Шесть()");
КонецФункции 

Функция Ответ()
	Возврат Вычислить("Глобал + СорокДва(7)");
КонецФункции 

Процедура ТестДолжен_ПроверитьСложныеВложенныеВызовыВычислить() Экспорт
	Глобал = 0;
	Рез = Вычислить("Ответ()");	
    юТест.ПроверитьРавенство(42, Рез);
	
	Глобал = 66;
	Рез = Вычислить("Ответ()");	
    юТест.ПроверитьРавенство(108, Рез);
КонецПроцедуры

Процедура ТестДолжен_ПроверитьВычислитьВнутриВыполнить() Экспорт
	Рез = -1;
	Выполнить("Для й=1 По 9 Цикл ц = -й; КонецЦикла; Рез = Вычислить(""ц + й"")");
    юТест.ПроверитьРавенство(1, Рез);
КонецПроцедуры

Процедура ТестДолжен_ПроверитьАлиасыФункцийИПеременныхвВычислить() Экспорт
	Рез = Вычислить("Лев(""фыв""+Символы.ПС,1)+Left(""fgh""+Chars.LF,1)");
    юТест.ПроверитьРавенство("фf", Рез);
КонецПроцедуры

Процедура ТестДолжен_ПроверитьОбработкуИсключенияВВыполнить() Экспорт
    
	Рез = 1;
	Выполнить "
	|Попытка 
	|  Рез = 1/0;
	|Исключение
	|  Рез = -1;
	|КонецПопытки;";
    юТест.ПроверитьРавенство(-1, Рез);
    
КонецПроцедуры

Процедура ТестДолжен_ПроверитьРекурсивныйВызовВыполнить() Экспорт
    
	Рез = 1;
	Код = "Рез=Рез+1;";
	Для й = 1 По 10 Цикл
		Код = СтрЗаменить(Код, """", """""");
		Код = "Выполнить""" + Код + """;";
	КонецЦикла;
	Выполнить(Код);

    юТест.ПроверитьРавенство(2, Рез);
   
КонецПроцедуры

Процедура Третья(Пар)
	Выполнить "Рез = Рез + Пар;";
КонецПроцедуры

Процедура Вторая(Пар)
	Выполнить "Лок = Пар + 1; Третья(Лок);";
КонецПроцедуры

Процедура ТестДолжен_ПроверитьВложенныеВызовыВыполнить() Экспорт
    
	Рез = 1;
	Выполнить "Вторая(2);";
	
    юТест.ПроверитьРавенство(4, Рез);
   
КонецПроцедуры

Процедура СИсключением()
	Выполнить "
	|Попытка
	|  Рез = Рез + 8;
	|  ВызватьИсключение 0;
	|Исключение
	|  Рез = Рез + 9;
	|КонецПопытки;
	|Выполнить(""Рез = Рез + (-10)"");";
КонецПроцедуры

Процедура ТестДолжен_ПроверитьВызовыВыполнитьСПопытками() Экспорт
    
	Рез = "0";
	попытка
		Выполнить("
			|Лок=""1"";
			|Попытка
			|  Рез = 1/0;
			|Исключение
			|  Рез = Рез + Лок;
			|КонецПопытки;");
		Выполнить("
			|Попытка
			|  Рез = Рез + 2/0;
			|Исключение
			|  Выполнить(""Рез = Рез + 3"");
			|КонецПопытки;
			|Рез = Рез + 4;");
		Выполнить("
			|Попытка
			|  Рез = Рез + 5;
			|  СИсключением();
			|Исключение
			|  Рез = Рез + 6;
			|КонецПопытки;
			|Рез = Рез + 7;");
	исключение
		Рез = Рез+ "!" ;
	конецпопытки;
	Рез = Рез + "+";
		
    юТест.ПроверитьРавенство("0134589-107+", Рез);
   
КонецПроцедуры
