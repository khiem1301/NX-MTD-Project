// Mọi test chạy trên cùng một Test DB thật với ambient transaction chưa commit (cùng username/mã seed) —
// chạy song song gây deadlock và chặn khóa lẫn nhau, nên bắt buộc tuần tự.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
