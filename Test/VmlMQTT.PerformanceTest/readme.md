# EMQX Performance Test Tool

Công cụ kiểm thử hiệu năng MQTT Broker để đánh giá khả năng chịu tải và hiệu suất của hệ thống EMQX, sử dụng dữ liệu người dùng thực từ database.

## Tổng quan

Công cụ này thực hiện quy trình kiểm thử hiệu năng đầy đủ cho hệ thống EMQX MQTT, bao gồm:

1. Truy vấn dữ liệu người dùng từ database PostgreSQL
2. Xác thực người dùng thông qua API (LoginByPassword và StartSession)
3. Thiết lập kết nối MQTT với thông tin xác thực từ API
4. Kiểm thử khả năng gửi/nhận tin nhắn trên các topic
5. Thu thập và phân tích kết quả

## Cài đặt

### Yêu cầu hệ thống

- Python 3.7 trở lên
- Kết nối đến cơ sở dữ liệu PostgreSQL
- API xác thực hoạt động
- EMQX MQTT Broker đang chạy

### Cài đặt các gói phụ thuộc

```bash
pip install paho-mqtt aiohttp psycopg2-binary pycryptodome
```

## Cấu hình

Chỉnh sửa file `config.json` để phù hợp với môi trường kiểm thử của bạn:

- `max_workers`: Số luồng worker tối đa chạy đồng thời
- `message_count`: Số lượng tin nhắn mỗi client sẽ gửi
- `message_interval`: Khoảng thời gian giữa các tin nhắn (giây)
- `message_size`: Kích thước tin nhắn (bytes)
- `qos`: Quality of Service (0, 1, 2)
- `connection_timeout`: Timeout kết nối (giây)
- `test_duration`: Thời gian chạy kiểm thử (giây)
- `use_cache`: Sử dụng dữ liệu cache nếu có
- `user_password`: Mật khẩu để truy vấn người dùng từ database
- `database`: Cấu hình kết nối database
- `api_config`: Cấu hình API
- `mqtt_config`: Cấu hình MQTT
- `scenarios`: Các kịch bản kiểm thử

## Quy trình xác thực và kết nối MQTT

Quá trình xác thực và kết nối tuân theo các bước sau:

1. **Truy vấn người dùng từ database** - Lấy danh sách người dùng có mật khẩu phù hợp
2. **Đăng nhập (LoginByPassword)** - Xác thực người dùng và lấy access_token và refresh_token
3. **Khởi tạo phiên (StartSession)** - Sử dụng access_token để lấy thông tin kết nối MQTT
4. **Kết nối MQTT** sử dụng:
   - ClientId và Username = refresh_token (từ API LoginByPassword)
   - Password = accessToken (từ API StartSession)
   - Host, Port, PubTopics, SubTopics từ API StartSession

## Sử dụng

### Chạy kiểm thử cơ bản

```bash
python main.py
```

### Chạy một kịch bản cụ thể

```bash
python main.py --scenario basic
```

### Chạy tất cả các kịch bản

```bash
python main.py --all
```

### Chỉ định số lượng người dùng và worker

```bash
python main.py --users 50 --workers 20
```

### Sử dụng mật khẩu khác để truy vấn người dùng

```bash
python main.py --password YOUR_PASSWORD_HERE
```

### Sử dụng file cấu hình khác

```bash
python main.py --config my_config.json
```

## Cấu trúc dự án

```
emqx_performance_test/
│
├── db_utils.py           # Truy vấn dữ liệu từ database
├── api_client.py         # Xử lý các cuộc gọi API xác thực
├── mqtt_client.py        # Kết nối và thực hiện test MQTT
├── test_executor.py      # Thực thi các test case
├── utils.py              # Các hàm tiện ích  
├── config.json           # Cấu hình hệ thống
├── main.py               # Điểm vào chính của ứng dụng
├── cache/                # Lưu trữ thông tin kết nối để tái sử dụng
└── results/              # Kết quả kiểm thử
```

## Kết quả kiểm thử

Kết quả kiểm thử được lưu trong thư mục `results/` dưới dạng file JSON với các thông tin:

- Tổng số người dùng
- Số người dùng xác thực thành công
- Số người dùng kết nối MQTT thành công
- Thời gian kết nối trung bình
- Số lượng tin nhắn đã gửi/nhận
- Chi tiết các lỗi phát sinh

## Lưu trữ thông tin kết nối

Thông tin người dùng được lưu trong thư mục `cache/` để có thể tái sử dụng cho các lần kiểm thử sau. Điều này giúp tiết kiệm thời gian khi không cần phải truy vấn database mỗi lần chạy test. Sử dụng tùy chọn `use_cache: false` trong config để buộc truy vấn mới.

## Lưu ý quan trọng

- **Kết nối database**: Đảm bảo thông tin kết nối database trong `config.json` là chính xác
- **Mật khẩu người dùng**: Mật khẩu để truy vấn người dùng cần được cấu hình đúng (thường là 'AB5717028198B5EF')
- **API endpoints**: Đảm bảo base_url API trong cấu hình trỏ đến môi trường đúng
- **Xác thực MQTT**: Đảm bảo rằng EMQX broker được cấu hình để chấp nhận xác thực với refresh_token và session token
- **Topics PubSub**: Công cụ sẽ đăng ký và gửi tin nhắn đến các topic được trả về từ API StartSession

## Xử lý lỗi

Công cụ ghi log chi tiết về quá trình test trong `logs/emqx_test.log`. Nếu gặp lỗi, hãy kiểm tra file log này để biết chi tiết.
