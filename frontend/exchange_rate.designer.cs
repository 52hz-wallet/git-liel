namespace WinFormsApp_ant.UIPlugins
{
    partial class exchange_rate : BasePluginForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.startDatePicker = new System.Windows.Forms.DateTimePicker();
            this.endDatePicker = new System.Windows.Forms.DateTimePicker();
            this.chartPlot = new ScottPlot.WinForms.FormsPlot();
            this.dataGridView = new System.Windows.Forms.DataGridView();
            this.refreshButton = new System.Windows.Forms.Button();
            this.labelStartDate = new System.Windows.Forms.Label();
            this.labelEndDate = new System.Windows.Forms.Label();
            this.labelChart = new System.Windows.Forms.Label();
            this.labelTable = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelStartDate
            // 
            this.labelStartDate.AutoSize = true;
            this.labelStartDate.Location = new System.Drawing.Point(12, 15);
            this.labelStartDate.Name = "labelStartDate";
            this.labelStartDate.Size = new System.Drawing.Size(67, 13);
            this.labelStartDate.TabIndex = 0;
            this.labelStartDate.Text = "开始日期：";
            // 
            // startDatePicker
            // 
            this.startDatePicker.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.startDatePicker.Location = new System.Drawing.Point(85, 12);
            this.startDatePicker.Name = "startDatePicker";
            this.startDatePicker.Size = new System.Drawing.Size(120, 20);
            this.startDatePicker.TabIndex = 1;
            this.startDatePicker.ValueChanged += new System.EventHandler(this.DatePicker_ValueChanged);
            // 
            // labelEndDate
            // 
            this.labelEndDate.AutoSize = true;
            this.labelEndDate.Location = new System.Drawing.Point(220, 15);
            this.labelEndDate.Name = "labelEndDate";
            this.labelEndDate.Size = new System.Drawing.Size(67, 13);
            this.labelEndDate.TabIndex = 2;
            this.labelEndDate.Text = "结束日期：";
            // 
            // endDatePicker
            // 
            this.endDatePicker.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.endDatePicker.Location = new System.Drawing.Point(293, 12);
            this.endDatePicker.Name = "endDatePicker";
            this.endDatePicker.Size = new System.Drawing.Size(120, 20);
            this.endDatePicker.TabIndex = 3;
            this.endDatePicker.ValueChanged += new System.EventHandler(this.DatePicker_ValueChanged);
            // 
            // refreshButton
            // 
            this.refreshButton.Location = new System.Drawing.Point(430, 10);
            this.refreshButton.Name = "refreshButton";
            this.refreshButton.Size = new System.Drawing.Size(75, 23);
            this.refreshButton.TabIndex = 4;
            this.refreshButton.Text = "刷新数据";
            this.refreshButton.UseVisualStyleBackColor = true;
            this.refreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            // 
            // labelChart
            // 
            this.labelChart.AutoSize = true;
            this.labelChart.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.labelChart.Location = new System.Drawing.Point(12, 45);
            this.labelChart.Name = "labelChart";
            this.labelChart.Size = new System.Drawing.Size(92, 15);
            this.labelChart.TabIndex = 5;
            this.labelChart.Text = "汇率走势图：";
            // 
            // chartPlot
            // 
            this.chartPlot.Location = new System.Drawing.Point(12, 65);
            this.chartPlot.Name = "chartPlot";
            this.chartPlot.Size = new System.Drawing.Size(800, 300);
            this.chartPlot.TabIndex = 6;
            // 
            // labelTable
            // 
            this.labelTable.AutoSize = true;
            this.labelTable.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold);
            this.labelTable.Location = new System.Drawing.Point(12, 375);
            this.labelTable.Name = "labelTable";
            this.labelTable.Size = new System.Drawing.Size(200, 15);
            this.labelTable.TabIndex = 7;
            this.labelTable.Text = "参考汇率中间价与估值汇率差值：";
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView.Location = new System.Drawing.Point(12, 395);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            this.dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView.Size = new System.Drawing.Size(800, 200);
            this.dataGridView.TabIndex = 8;
            // 
            // exchange_rate
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(830, 610);
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.labelTable);
            this.Controls.Add(this.chartPlot);
            this.Controls.Add(this.labelChart);
            this.Controls.Add(this.refreshButton);
            this.Controls.Add(this.endDatePicker);
            this.Controls.Add(this.labelEndDate);
            this.Controls.Add(this.startDatePicker);
            this.Controls.Add(this.labelStartDate);
            this.Name = "exchange_rate";
            this.Text = "港股历史汇率走势";
            this.Load += new System.EventHandler(this.exchange_rate_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelStartDate;
        private System.Windows.Forms.DateTimePicker startDatePicker;
        private System.Windows.Forms.Label labelEndDate;
        private System.Windows.Forms.DateTimePicker endDatePicker;
        private System.Windows.Forms.Button refreshButton;
        private System.Windows.Forms.Label labelChart;
        private ScottPlot.WinForms.FormsPlot chartPlot;
        private System.Windows.Forms.Label labelTable;
        private System.Windows.Forms.DataGridView dataGridView;
    }
}