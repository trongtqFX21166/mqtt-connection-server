import json
import os
import logging
import time
from typing import Dict, List, Any, Optional

# Thiết lập logging
def setup_logging(log_file="emqx_test.log", console_level=logging.INFO, file_level=logging.DEBUG):
    """
    Thiết lập logging
    
    Args:
        log_file: Đường dẫn file log
        console_level: Mức độ log cho console
        file_level: Mức độ log cho file
    """
    # Tạo thư mục logs nếu chưa tồn tại
    log_dir = "logs"
    os.makedirs(log_dir, exist_ok=True)
    
    log_path = os.path.join(log_dir, log_file)
    
    # Cấu hình root logger
    logger = logging.getLogger()
    logger.setLevel(logging.DEBUG)  # Cấp thấp nhất để bắt tất cả
    
    # Định dạng log
    formatter = logging.Formatter('%(asctime)s - %(name)s - %(levelname)s - %(message)s')
    
    # Handler cho console
    console_handler = logging.StreamHandler()
    console_handler.setLevel(console_level)
    console_handler.setFormatter(formatter)
    
    # Handler cho file
    file_handler = logging.FileHandler(log_path, encoding='utf-8')
    file_handler.setLevel(file_level)
    file_handler.setFormatter(formatter)
    
    # Thêm handlers
    logger.addHandler(console_handler)
    logger.addHandler(file_handler)
    
    return logger

def load_config(config_file="config.json") -> Dict[str, Any]:
    """
    Đọc file cấu hình JSON
    
    Args:
        config_file: Đường dẫn file cấu hình
        
    Returns:
        Dict: Cấu hình dưới dạng dictionary
    """
    try:
        if os.path.exists(config_file):
            with open(config_file, 'r', encoding='utf-8') as f:
                config = json.load(f)
            return config
        else:
            # Tạo cấu hình mặc định
            default_config = {
                "max_workers": 10,
                "message_count": 10,
                "message_interval": 1.0,
                "message_size": 100,
                "qos": 1,
                "connection_timeout": 30,
                "test_duration": 60,
                "broker": {
                    "host": "localhost",
                    "port": 1883
                }
            }
            
            # Lưu cấu hình mặc định
            with open(config_file, 'w', encoding='utf-8') as f:
                json.dump(default_config, f, indent=2)
                
            return default_config
    except Exception as e:
        logging.error(f"Lỗi khi đọc file cấu hình: {str(e)}")
        # Trả về cấu hình mặc định nếu có lỗi
        return {
            "max_workers": 10,
            "message_count": 10,
            "message_interval": 1.0,
            "message_size": 100,
            "qos": 1,
            "connection_timeout": 30,
            "test_duration": 60
        }

def save_cache(data: Dict[str, Any], filename: str):
    """
    Lưu dữ liệu vào file cache
    
    Args:
        data: Dữ liệu cần lưu
        filename: Tên file
    """
    cache_dir = "cache"
    os.makedirs(cache_dir, exist_ok=True)
    
    file_path = os.path.join(cache_dir, filename)
    
    try:
        with open(file_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2)
        return True
    except Exception as e:
        logging.error(f"Lỗi khi lưu cache {filename}: {str(e)}")
        return False

def load_cache(filename: str) -> Optional[Dict[str, Any]]:
    """
    Đọc dữ liệu từ file cache
    
    Args:
        filename: Tên file
        
    Returns:
        Dict: Dữ liệu cache hoặc None nếu không tìm thấy
    """
    cache_dir = "cache"
    file_path = os.path.join(cache_dir, filename)
    
    try:
        if os.path.exists(file_path):
            with open(file_path, 'r', encoding='utf-8') as f:
                return json.load(f)
        return None
    except Exception as e:
        logging.error(f"Lỗi khi đọc cache {filename}: {str(e)}")
        return None

def save_connection_info(phone: str, connection_info: Dict[str, Any]):
    """
    Lưu thông tin kết nối MQTT của một người dùng
    
    Args:
        phone: Số điện thoại người dùng
        connection_info: Thông tin kết nối
    """
    filename = f"connection_{phone}.json"
    return save_cache(connection_info, filename)

def load_connection_info(phone: str) -> Optional[Dict[str, Any]]:
    """
    Đọc thông tin kết nối MQTT của một người dùng
    
    Args:
        phone: Số điện thoại người dùng
        
    Returns:
        Dict: Thông tin kết nối hoặc None nếu không tìm thấy
    """
    filename = f"connection_{phone}.json"
    return load_cache(filename)

def save_connection_batch(connections: Dict[str, Dict[str, Any]], batch_name: str = "batch"):
    """
    Lưu thông tin kết nối của nhiều người dùng
    
    Args:
        connections: Map phone -> connection_info
        batch_name: Tên batch
    """
    timestamp = time.strftime("%Y%m%d_%H%M%S")
    filename = f"{batch_name}_{timestamp}.json"
    return save_cache(connections, filename)

def print_progress(completed: int, total: int, prefix: str = "Tiến độ"):
    """
    In tiến độ thực hiện
    
    Args:
        completed: Số lượng đã hoàn thành
        total: Tổng số lượng
        prefix: Tiền tố hiển thị
    """
    percent = (completed / total) * 100 if total > 0 else 0
    bar_length = 50
    filled_length = int(bar_length * completed // total)
    
    bar = '█' * filled_length + '-' * (bar_length - filled_length)
    print(f"\r{prefix}: |{bar}| {percent:.1f}% ({completed}/{total})", end='')
    
    if completed == total:
        print()  # Xuống dòng khi hoàn thành

def format_summary(result: Dict[str, Any]) -> str:
    """
    Định dạng kết quả test để hiển thị
    
    Args:
        result: Kết quả test
        
    Returns:
        str: Chuỗi đã định dạng
    """
    duration = result.get("duration", 0)
    success_rates = result.get("success_rates", {})
    
    summary = [
        "===== KẾT QUẢ TEST =====",
        f"Tổng số người dùng: {result.get('total_users', 0)}",
        f"Xác thực thành công: {result.get('successful_auth', 0)} ({success_rates.get('auth_rate', 0):.1f}%)",
        f"Kết nối MQTT thành công: {result.get('successful_connect', 0)} ({success_rates.get('connect_rate', 0):.1f}%)",
        f"Thời gian kết nối trung bình: {result.get('avg_connection_time', 0):.3f} giây",
        f"Số tin nhắn đã gửi: {result.get('messages_published', 0)}",
        f"Số tin nhắn đã nhận: {result.get('messages_received', 0)}",
        f"Thời gian test: {duration:.2f} giây"
    ]
    
    # Thêm thông tin lỗi nếu có
    errors = result.get("errors", {})
    if errors:
        summary.append("\nLỗi:")
        for error, count in errors.items():
            summary.append(f"  - {error}: {count}")
    
    return "\n".join(summary)

def clear_session_cache():
    """
    Xóa tất cả các file cache session API
    
    Returns:
        bool: True nếu xóa thành công, False nếu có lỗi
    """
    import shutil
    try:
        if os.path.exists("session_cache"):
            shutil.rmtree("session_cache")
            os.makedirs("session_cache")
            logging.info("Đã xóa tất cả cache session API")
        return True
    except Exception as e:
        logging.error(f"Lỗi khi xóa cache session API: {str(e)}")
        return False

def get_session_cache_files() -> List[str]:
    """
    Lấy danh sách các file cache session API
    
    Returns:
        List[str]: Danh sách các file cache
    """
    if not os.path.exists("session_cache"):
        return []
        
    return [f for f in os.listdir("session_cache") if f.endswith(".json")]

def get_session_cache_stats() -> Dict[str, int]:
    """
    Lấy thống kê về cache session API
    
    Returns:
        Dict[str, int]: Thống kê về cache
    """
    files = get_session_cache_files()
    return {
        "total_files": len(files),
        "total_size": sum(os.path.getsize(os.path.join("session_cache", f)) for f in files)
    }