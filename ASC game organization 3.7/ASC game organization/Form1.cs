using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Collections;
using System.Threading;

namespace ASC_game_organization
{
    public partial class Form1 : Form
    {
        string currentDay = getCurrentDayOfWeek();
        //Boolean shouldUpdateGamesPlayedValue = true;

        // Contains a unique list of people. Used to ensure a person only gets one recess per day.
        List<int> uniquePersonIDList = new List<int>();

        // Contains a list of all employees eligible for a recess for a given timeslot. 
        List<Player> eligiblePlayersAllDepartmentsOneTimeSlot = new List<Player>();

        // Connection string used to connect to database.
        string myConnectionString = @"server=sql5.freemysqlhosting.net;user id=sql584334;Password=iK6*xA7!;database=sql584334";

        // Determines how many recesses a person is allowed per month.
        const int monthlyRecessLimit = 7;

        // Initializes all GUI components. 
        public Form1()
        {
            InitializeComponent();
        }

        // Starting point of the application.
        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;

            // erase list containing people that have been picked today. This is needed if the application has already been launched once, and the user presses the "update tables" button twice.
            uniquePersonIDList.Clear();

            button1.BeginInvoke(
                    new Action(() =>
                    {
                        // Clear all the listviews containing people picked to go play. This is needed if the application has already been launched once, and the user presses the "update tables" button twice.
                        playerListView800to830.Items.Clear();
                        playerListView830to900.Items.Clear();
                        playerListView900to930.Items.Clear();
                        playerListView930to1000.Items.Clear();
                        playerListView1500to1530.Items.Clear();
                        playerListView1530to1600.Items.Clear();
                        playerListView1600to1630.Items.Clear();
                        playerListView1630to1700.Items.Clear();
                        playerListView1700to1730.Items.Clear();
                        playerListView1730to1800.Items.Clear();
                        playerListView1800to1830.Items.Clear();
                        playerListView1830to1900.Items.Clear();
                    }
                    ));

            //int numberOfTutorsAtWork;
            currentDayTxt.Text = currentDay.ToUpper() + " " + DateTime.Now.ToString("MM/dd");

            // Get current date in yyyyMMdd format
            string currentDate = getCurrentDate();

            // get last launch date from database
            string lastAppLaunchDate = getLastAppLaunchDate();

            // set boolean value used to determine whether or not numberOfGamesPlayed should be updated for eligible players. 
            // Used to determine if app has already been launched once today. If it has, there is no need to update games_played values again.
            if (lastAppLaunchDate.Equals(currentDate))
            {
                Thread backgroundThread = new Thread(
                new ThreadStart(() =>
                {
                    loadPeopleFromTableTodaysRecessPeople();
                }
                    ));
                backgroundThread.Start();

                button1.Enabled = true;
            }
            else
            {
                // truncate todaysRecessPeople table
                truncateTable("todaysrecesspeople");

                string[] departmentsArray = { "welcomedesk", "chemistry", "testingcenter", "asl", "computerprogramming", "economics", "foreignlanguage", "communication", "msc_ia", "management", "msc", "spa", "music", "accounting", "ost", "histgovt", "physics", "biology", "gen_tut_ia" };

                // parallel arrays
                int[] shiftStartArray = { 800, 830, 900, 930, 1500, 1530, 1600, 1630, 1700, 1730, 1800, 1830 };
                int[] shiftEndArray = { 830, 900, 930, 1000, 1530, 1600, 1630, 1700, 1730, 1800, 1830, 1900 };

                // Configure the progress bar
                int progressBarMax = shiftStartArray.Length * departmentsArray.Length;
                progressBar.Minimum = 0;
                progressBar.Maximum = progressBarMax;

                // Start a background thread that does all the heavy work. This stops the GUI from locking up.
                Thread backgroundThread = new Thread(
                new ThreadStart(() =>
                {
                    for (int j = 0; j < shiftStartArray.Length; j++)
                    {
                        for (int i = 0; i < departmentsArray.Length; i++)
                        {
                            int minEmployeesNeeded = getHourlyMinRequiredStaff(departmentsArray[i], shiftStartArray[j]);
                            int numberOfTutorsAtWork = getNumberOfTutorsAtWork(departmentsArray[i], shiftStartArray[j], shiftEndArray[j], currentDay);
                            getPlayers(departmentsArray[i], shiftStartArray[j], shiftEndArray[j], numberOfTutorsAtWork, minEmployeesNeeded);

                            progressBar.BeginInvoke(
                            new Action(() =>
                            {
                                progressBar.Increment(1);

                                if (i < departmentsArray.Length)
                                {
                                    progressLbl.Text = "Processing " + shiftStartArray[j] + "-" + shiftEndArray[j] + " timeslot for " + departmentsArray[i] + " department. " + (j + 1) + "/" + (shiftStartArray.Length);
                                }

                                if (j >= shiftStartArray.Length)
                                {
                                    progressLbl.Text = "Done.";
                                    progressBar.Value = 0;
                                }
                            }
                            ));
                        }

                        // Check if, across all departments, only one person has been picked to get a recess. 
                        // In that case, don't do anything with that person. Else, add everyone to the list and increase games_played, add person to uniqueListID
                        if (eligiblePlayersAllDepartmentsOneTimeSlot.Count > 1)
                        {
                            for (int i = 0; i < eligiblePlayersAllDepartmentsOneTimeSlot.Count; i++)
                            {
                                int randomUniqueId = eligiblePlayersAllDepartmentsOneTimeSlot[i].RanUniqueId;
                                int numberOfGamesPlayedPerMonth = eligiblePlayersAllDepartmentsOneTimeSlot[i].GamesPlayed;

                                /* This Boolean determines whether or not a user's games_played value should be incremented. If the app is launched twice (or more) 
                                times in one day there is no need to increment games_played, because it has already happened once.*/
                                //if (shouldUpdateGamesPlayedValue)
                                //{
                                // Update number of games played for current user
                                numberOfGamesPlayedPerMonth = updateNumberOfGamesPlayed(randomUniqueId, numberOfGamesPlayedPerMonth);
                                //}

                                string name = eligiblePlayersAllDepartmentsOneTimeSlot[i].Name;
                                string department = eligiblePlayersAllDepartmentsOneTimeSlot[i].Department;
                                int shiftStart = eligiblePlayersAllDepartmentsOneTimeSlot[i].ShiftStart;
                                int shiftEnd = eligiblePlayersAllDepartmentsOneTimeSlot[i].ShiftEnd;

                                // Add the person to the graphical user interface
                                addPersonToGUI(name, department, shiftStart, numberOfGamesPlayedPerMonth);

                                // Add person to list that keeps track of all the people that have had a recess today.
                                uniquePersonIDList.Add(randomUniqueId);

                                // Add person to table todaysRecessPeople
                                addPersonToTodaysRecessPeopleTable(name, department, shiftStart, numberOfGamesPlayedPerMonth);
                            }
                        }

                        // Clear the list to get ready for the next time slot.
                        eligiblePlayersAllDepartmentsOneTimeSlot.Clear();
                    }

                    // write current date to 'meta' table
                    writeCurrentDateToMetaTable();

                    button1.BeginInvoke(
                        new Action(() =>
                        {
                            button1.Enabled = true;
                        }
                        ));
                }
                    ));
                backgroundThread.Start();
            }


        } // End button1_click method

        // Queries a table to figure out what was the last date the application was launched. This is needed to avoid incrementing the count of a person's recesses, if the application is relaunched twice or more times in a day.
        private string getLastAppLaunchDate()
        {
            string date = "";

            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "SELECT lastdatelaunch FROM meta WHERE id = 1";

                MySqlCommand cmd = new MySqlCommand(stm, conn);

                date = (String)cmd.ExecuteScalar();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }
            }

            return date;
        }

        // Writes the current date that the application was launched to a table. This is needed to avoid incrementing the count of a person's recesses, if the application is relaunched twice or more times in a day.
        private void writeCurrentDateToMetaTable()
        {
            string currentDate = getCurrentDate();

            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "UPDATE meta SET lastdatelaunch = @currentDate WHERE ID = 1";
                MySqlCommand cmd = new MySqlCommand(stm, conn);

                cmd.Parameters.AddWithValue("@currentDate", currentDate);

                int rowsAffected = cmd.ExecuteNonQuery();
                //MessageBox.Show("Rows affected: " + rowsAffected);
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }
            }


        }

        // Get the current date in YYYYMMDD format. E.g. 20150801 for August 1st, 2015.
        private string getCurrentDate()
        {
            string date = "";

            date = DateTime.Now.ToString("yyyyMMdd");

            return date;
        }

        // Get the current day of week. Monday = "monday". Friday = "friday".
        private static string getCurrentDayOfWeek()
        {
            string currentDay = "";

            currentDay = DateTime.Now.DayOfWeek.ToString().ToLower();

            return currentDay;
        }

        // Updates the number of recesses a person has had.
        private int updateNumberOfGamesPlayed(int ranUniqueId, int numberOfGamesPlayedPerMonth)
        {
            string currentMonth = getCurrentMonthAbbreviated();
            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "UPDATE games_played_per_month SET " + currentMonth + " = @numberOfGamesPlayedPerMonth WHERE ranuniqueid = @id";
                MySqlCommand cmd = new MySqlCommand(stm, conn);

                // Increase number of games played by one, so that we can write that new value to the user's games_played attribute
                numberOfGamesPlayedPerMonth++;
                cmd.Parameters.AddWithValue("@numberOfGamesPlayedPerMonth", numberOfGamesPlayedPerMonth);
                cmd.Parameters.AddWithValue("@id", ranUniqueId);

                int rowsAffected = cmd.ExecuteNonQuery();
                //MessageBox.Show("Rows affected: " + rowsAffected);
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }
            }

            return numberOfGamesPlayedPerMonth;
        }

        // Query a table to figure out how many people are at work for a given departments and half-hour interval.
        private int getNumberOfTutorsAtWork(string department, int shiftStart, int shiftEnd, string currentDay)
        {
            Int32 numberOfTutorsAtwork = -1;

            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "SELECT COUNT(1) FROM " + department + " WHERE " + currentDay + "_start != 0 && " + currentDay + "_start <= " + shiftStart + " && " + currentDay + "_end >= " + shiftEnd;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                numberOfTutorsAtwork = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }

                if (numberOfTutorsAtwork == -1)
                {
                    MessageBox.Show("Error: Unable to get number of tutors at work.");
                }
            }

            return numberOfTutorsAtwork;

        }

        // Looks through a list containing all the people who have been picked for a recess today. Used to ensure that a person only gets on recess per day.
        private Boolean hasAlreadyHadARecessToday(int uniquePersonID)
        {
            foreach (int person in uniquePersonIDList)
            {
                if (person == uniquePersonID)
                {
                    // If a person has already been picked once today, exit out of the function, so that they do not get another recess.
                    return true;
                }
            }

            return false;
        }

        // Get the current month abbreviated to three characters. January = jan, August = aug...
        private string getCurrentMonthAbbreviated()
        {
            // Get the current month abbreviated to 3 letters. E.g. "jan", "jul" etc.
            return DateTime.Now.ToString("MMM").ToLower();
        }

        // Queries a database to figure out how many recesses a person has had during the current month.
        private int getNumberOfGamesPlayedPerMonth(int ranUniqueId)
        {
            /* Add the person's ranUniqueId to the games_played_per_month table, because I'm lazy and don't want to do it manually.
             * Will insert the person's ranUniqueID into the table if it does not exist, or do nothing if it does exist.
             */
            addRanUniqueIdToGamesPlayedPerMonthTable(ranUniqueId);

            // Get the current month abbreviated to 3 letters. E.g. "jan", "jul" etc.
            string month = getCurrentMonthAbbreviated();
            int numberOfRecessesPerMonth = -1;
            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "SELECT " + month + "  FROM games_played_per_month WHERE ranuniqueid = " + ranUniqueId;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                numberOfRecessesPerMonth = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }

                if (numberOfRecessesPerMonth == -1)
                {
                    MessageBox.Show("Error: Unable to get number games played per month.");
                }
            }

            return numberOfRecessesPerMonth;
        }

        // Adds a person's unique ID to a database table. Used to keep track of how many recesses a person has had per month.
        private void addRanUniqueIdToGamesPlayedPerMonthTable(int ranUniqueId)
        {
            /* Add the person's ranUniqueId to the games_played_per_month table, because I'm lazy and don't want to do it manually.
             * Will insert the person's ranUniqueID into the table if it does not exist. Or do nothin if it does exist.
             */
            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT IGNORE INTO games_played_per_month SET ranuniqueid = " + ranUniqueId;
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {

                MessageBox.Show("Error: " + ex.ToString());
            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }
            }

        }

        // Queries the database for eligible players.
        private void getPlayers(string department, int shiftStart, int shiftEnd, int numberOfTutorsAtwork, int minTutorsNeeded)
        {
            int limit = numberOfTutorsAtwork - minTutorsNeeded;
            List<Player> eligiblePlayersOneDepartment = new List<Player>();

            // If limit is 0 it means that no tutors are expendable at the current time. Thus there is no need to do any SQL queries.
            if (limit > 0)
            {
                MySqlConnection conn = null;
                MySqlDataReader rdr = null;

                try
                {
                    conn = new MySqlConnection(myConnectionString);
                    conn.Open();

                    string stm = "SELECT name, " + currentDay + "_start, " + currentDay + "_end, " + "ranuniqueid " + " FROM " + department + " WHERE " + currentDay + "_start != 0 && " + currentDay + "_start <= " + shiftStart + " && " + currentDay + "_end >= " + shiftEnd;

                    MySqlCommand cmd = new MySqlCommand(stm, conn);
                    rdr = cmd.ExecuteReader();

                    while (rdr.Read())
                    {
                        string name = rdr.GetString(0);
                        int ranUniqueId = rdr.GetInt32(3);
                        int numberOfGamesPlayedPerMonth = getNumberOfGamesPlayedPerMonth(ranUniqueId);

                        // Check if this person has already been scheduled for a recess today. If he hasn't, the IF statement is true, and the body will execute.
                        // Also, check if this person has had already reached the monthly limit of recesses.
                        if (!hasAlreadyHadARecessToday(ranUniqueId) && numberOfGamesPlayedPerMonth <= monthlyRecessLimit)
                        {
                            // This dictionary contains a list of all eligible players. It will be sorted based on number of games played.
                            eligiblePlayersOneDepartment.Add(new Player(name, department, numberOfGamesPlayedPerMonth, ranUniqueId, shiftStart, shiftEnd));
                        }
                    }
                }
                catch (MySqlException ex)
                {
                    MessageBox.Show("Error: " + ex.ToString());
                }
                finally
                {
                    if (rdr != null)
                    {
                        rdr.Close();
                    }

                    if (conn != null)
                    {
                        conn.Close();
                    }
                }

                // Sort list
                eligiblePlayersOneDepartment.Sort();

                for (int i = 0; i < limit && i < eligiblePlayersOneDepartment.Count; i++)
                {
                    string name = eligiblePlayersOneDepartment[i].Name;
                    int numberOfGamesPlayedPerMonth = eligiblePlayersOneDepartment[i].GamesPlayed;
                    int randomUniqueId = eligiblePlayersOneDepartment[i].RanUniqueId;

                    eligiblePlayersAllDepartmentsOneTimeSlot.Add(new Player(name, department, numberOfGamesPlayedPerMonth, randomUniqueId, shiftStart, shiftEnd));
                }

                // Clear the list of eligible players, so it's ready to be filled with the next department's employees.
                eligiblePlayersOneDepartment.Clear();
            }
        }

        // Adds a person to the graphical user interface.
        private void addPersonToGUI(string name, string department, int shiftStart, int numberOfGamesPlayedPerMonth)
        {
            // Make the department name look nice
            department = formatDepartmentName(department);

            // This string array is used to fill out information in the GUI (ListView component requires additional columns to be saved in an array)
            string[] additionalPlayerInfo = { name, department };

            // Add user's info to GUI.
            switch (shiftStart)
            {
                case 800:
                    playerListView800to830.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView800to830.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 830:
                    playerListView830to900.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView830to900.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 900:
                    playerListView900to930.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView900to930.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 930:
                    playerListView930to1000.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView930to1000.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1500:
                    playerListView1500to1530.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1500to1530.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1530:
                    playerListView1530to1600.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1530to1600.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1600:
                    playerListView1600to1630.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1600to1630.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1630:
                    playerListView1630to1700.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1630to1700.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1700:
                    playerListView1700to1730.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1700to1730.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1730:
                    playerListView1730to1800.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1730to1800.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1800:
                    playerListView1800to1830.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1800to1830.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
                case 1830:
                    playerListView1830to1900.BeginInvoke(
                    new Action(() =>
                    {
                        playerListView1830to1900.Items.Add(numberOfGamesPlayedPerMonth.ToString()).SubItems.AddRange(additionalPlayerInfo);
                    }
                    ));
                    break;
            }
        }

        // A function that queries a database table to find out the minimum required staff on duty for a given department and half-hour interval.
        private int getHourlyMinRequiredStaff(string department, int shiftStart)
        {
            int hourlyMinStaff = -1;
            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "SELECT minstaff FROM hourlyminstaffonduty WHERE department = '" + department + "' AND time = " + shiftStart;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                hourlyMinStaff = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }

                if (hourlyMinStaff == -1)
                {
                    MessageBox.Show("Error: Unable to get min. number of staff on duty.");
                }
            }
            return hourlyMinStaff;
        }

        // These two events enable the mousewheel to scroll up/down in the form.
        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            panel1.Focus();
        }

        // This function formats the department names from their crude table name to a nicely readable string.
        private string formatDepartmentName(string department)
        {
            string formattedDepartmentName = "Error in formatDepartmentName function.";

            switch (department)
            {
                case "welcomedesk":
                    formattedDepartmentName = "Welcome Desk";
                    break;
                case "chemistry":
                    formattedDepartmentName = "Chemistry";
                    break;
                case "testingcenter":
                    formattedDepartmentName = "Testing Center";
                    break;
                case "asl":
                    formattedDepartmentName = "ASL";
                    break;
                case "computerprogramming":
                    formattedDepartmentName = "Computer Programming";
                    break;
                case "economics":
                    formattedDepartmentName = "Economics";
                    break;
                case "foreignlanguage":
                    formattedDepartmentName = "Foreign Language";
                    break;
                case "communication":
                    formattedDepartmentName = "Communication";
                    break;
                case "msc_ia":
                    formattedDepartmentName = "MSC IA";
                    break;
                case "management":
                    formattedDepartmentName = "Management";
                    break;
                case "msc":
                    formattedDepartmentName = "MSC";
                    break;
                case "spa":
                    formattedDepartmentName = "SPA";
                    break;
                case "music":
                    formattedDepartmentName = "Music";
                    break;
                case "accounting":
                    formattedDepartmentName = "Accounting";
                    break;
                case "ost":
                    formattedDepartmentName = "OST";
                    break;
                case "histgovt":
                    formattedDepartmentName = "Hist/Govt";
                    break;
                case "physics":
                    formattedDepartmentName = "Physics";
                    break;
                case "biology":
                    formattedDepartmentName = "Biology";
                    break;
                case "gen_tut_ia":
                    formattedDepartmentName = "Gen. Tut. IA";
                    break;
            }

            return formattedDepartmentName;
        }

        // This function will truncate (delete ALL records) from a table.
        private void truncateTable(string tableName)
        {
            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "TRUNCATE TABLE " + tableName;
                MySqlCommand cmd = new MySqlCommand(stm, conn);

                int rowsAffected = cmd.ExecuteNonQuery();
                //MessageBox.Show("Rows affected: " + rowsAffected);
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());

            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }
            }
        }

        // This function will add a person to the table called "todaysRecessPeople". This table is used to store todays picked people.
        private void addPersonToTodaysRecessPeopleTable(string name, string department, int shiftStart, int gamesPlayed)
        {
            MySqlConnection conn = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                MySqlCommand cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO todaysrecesspeople (name, department, shiftstart, games_played) VALUES (\"" + name + "\", " + "\"" + department + "\", " + shiftStart + ", " + gamesPlayed + ");";
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {

                MessageBox.Show("Error: " + ex.ToString());
            }
            finally
            {
                if (conn != null)
                {
                    conn.Close();
                }
            }
        }

        // This function will load people from the table called "todaysRecessPeople".
        private void loadPeopleFromTableTodaysRecessPeople()
        {
            MySqlConnection conn = null;
            MySqlDataReader rdr = null;

            try
            {
                conn = new MySqlConnection(myConnectionString);
                conn.Open();

                string stm = "SELECT name, department, shiftstart, games_played FROM todaysrecesspeople";

                MySqlCommand cmd = new MySqlCommand(stm, conn);
                rdr = cmd.ExecuteReader();

                while (rdr.Read())
                {
                    string name = rdr.GetString(0);
                    string department = rdr.GetString(1);
                    int shiftStart = rdr.GetInt32(2);
                    int games_played = rdr.GetInt32(3);

                    addPersonToGUI(name, department, shiftStart, games_played);
                }
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Error: " + ex.ToString());
            }
            finally
            {
                if (rdr != null)
                {
                    rdr.Close();
                }

                if (conn != null)
                {
                    conn.Close();
                }
            }
        }
    }
}
